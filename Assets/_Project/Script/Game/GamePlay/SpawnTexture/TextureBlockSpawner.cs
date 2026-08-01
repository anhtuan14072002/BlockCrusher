using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
using Collider = Unity.Physics.Collider;
using RenderMaterial = UnityEngine.Material;

public sealed partial class TextureBlockSpawner : MonoBehaviour
{
    private const int MaxInstancesPerBatch = 1023;
    private const int ReleasedMeshVariantCount = 4;
    private const float MinimumPhysicsDeltaTime = 0.001f;
    private const float MaxCellTravelPerStep = 0.75f;

    private static readonly List<TextureBlockSpawner> ActiveSpawners = new();
    private static readonly Dictionary<string, int> SuckedItemCounts = new();
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static int _nextOwnerId = 1;

    [Header("Source")]
    [SerializeField] private GameObject[] _levelPrefabs;
    [SerializeField, Min(1)] private int _startLevel = 1;
    [SerializeField] private Transform _container;
    [SerializeField] private RenderMaterial _chunkMaterial;
    [SerializeField] private bool _spawnOnAwake = true;

    [Header("Chunk Rendering")]
    [SerializeField, Range(8, 64)] private int _chunkSize = 24;
    [SerializeField, Range(1, 8)] private int _maxChunkRebuildsPerFrame = 2;

    [Header("Released Blocks")]
    [SerializeField] private float _sawReleaseRadius = 0.18f;       
    [SerializeField, Range(0f, 20f)] private float _releasedBlockDamping = 2.5f;
    [SerializeField, Range(0f, 20f)] private float _releasedBlockAngularDamping = 4f;
    [SerializeField, Range(0.01f, 10f)] private float _releasedBlockMass = 0.1f;
    [SerializeField, Range(128, 10000)] private int _maxReleasedPhysicsBlocks = 5000;
    [SerializeField, Range(1, 4)] private int _releasedBlockRenderInterval = 2;
    [SerializeField, Range(0f, 1f)] private float _physicsFriction = 0.12f;
    [SerializeField, Range(0f, 1f)] private float _physicsRestitution;
    [SerializeField] private Transform[] _releasedBlockWalls;

    [Header("Metaball Water")]
    [SerializeField] private bool _spawnMetaballWater = true;
    [SerializeField, Range(1f, 3f)] private float _waterParticleScale = 1.65f;
    [SerializeField] private ParticleSystem _metaballParticles;
    [SerializeField] private RenderMaterial _metaballSourceMaterial;

    private readonly List<ChunkRuntime> _chunks = new();
    private readonly List<int> _dirtyChunks = new(16);
    private readonly List<int> _scheduledChunkRebuilds = new(8);
    private readonly List<JobHandle> _scheduledChunkHandles = new(8);

    private readonly List<Entity> _releasedBlockEntities = new(1024);
    private readonly List<Entity> _metaballWaterEntities = new(64);
    private readonly List<Entity> _releasedBlockWallEntities = new(4);
    private readonly List<BlobAssetReference<Collider>> _releasedBlockWallColliders = new(4);
    private readonly List<ReleasedBlockRuntimeType> _releasedBlockTypes = new(4);

    private readonly RenderFrameData[] _renderFrames = new RenderFrameData[2];

    private NativeArray<Color32> _cellColors;
    private NativeArray<Color32> _cellReleasedColors;
    private NativeArray<byte> _cellSolid;
    private NativeArray<ushort> _cellReleasedTypes;

    private World _ecsWorld;
    private EntityManager _entityManager;
    private EntityQuery _releasedBlockQuery;
    private EntityArchetype _releasedBlockArchetype;
    private MaterialPropertyBlock _releasedBlockPropertyBlock;
    private ParticleSystem _cutParticles;
    private RenderMaterial _cutParticleMaterial;

    private byte[] _chunkDirty;
    private Transform _runtimeParent;
    private RenderMaterial _runtimeChunkMaterial;
    private Vector3 _offset;

    private int _ownerId;
    private int _gridWidth;
    private int _gridHeight;
    private int _chunkColumns;
    private float _cellSize;
    private float _chunkColliderDepth;
    private int _debrisSpawnSequence;

    private ParticleSystem.Particle[] _metaballParticleBuffer;
    private int _spawnedWaterCellCount;

    private bool _hasReleasedBlockQuery;
    private bool _hasPendingSawPush;
    private Vector3 _pendingSawCenter;
    private Vector3 _pendingSawDirection;
    private float _pendingSawSpeed;
    private float _pendingSawOutwardForce;
    private float _pendingSawTangentialForce;
    private float _pendingSawSpinDirection;
    private float _pendingSawRadius;
    private float _pendingSawMaxVelocity;

    private int _displayRenderFrame = -1;
    private int _scheduledRenderFrame = -1;

    private sealed class ReleasedBlockRuntimeType
    {
        public GameObject Prefab;
        public Mesh Mesh;
        public RenderMaterial Material;
        public BlobAssetReference<Collider> Collider;
        public float Scale;
        public float Radius;
        public FixedString64Bytes CollectibleId;
        public Matrix4x4[][] BatchMatrices;
        public Vector4[][] BatchColors;
        public int[] BatchCounts;
        public int BatchCount;
        public int RenderCount;
        public ushort FirstVariantIndex;
        public byte VariantCount;
        public bool OwnsMesh;
    }

    public static int SuckedBlockCount { get; private set; }
    public static IReadOnlyDictionary<string, int> SuckedItems => SuckedItemCounts;
    public static event System.Action<int> BlocksSucked;
    public static event System.Action<string, int> ItemSucked;

    public int CurrentLevel { get; private set; }
    public int LevelCount => _levelPrefabs?.Length ?? 0;

    public static int GetSuckedItemCount(string collectibleId) =>
        SuckedItemCounts.TryGetValue(collectibleId, out int count) ? count : 0;

    private void OnEnable()
    {
        if (_ownerId == 0)
            _ownerId = _nextOwnerId++;

        if (!ActiveSpawners.Contains(this))
            ActiveSpawners.Add(this);
    }

    private void OnDisable()
    {
        ActiveSpawners.Remove(this);
    }

    private void Awake()
    {
        SuckedBlockCount = 0;
        SuckedItemCounts.Clear();
        CurrentLevel = Mathf.Clamp(_startLevel, 1, Mathf.Max(1, LevelCount));
        if (_spawnOnAwake)
            Spawn();
    }

    public void LoadLevel(int levelNumber)
    {
        if (levelNumber < 1 || levelNumber > LevelCount)
        {
            Debug.LogError($"Level {levelNumber} is outside the configured range 1-{LevelCount}.", this);
            return;
        }

        CurrentLevel = levelNumber;
        Spawn();
    }

    private void OnDestroy()
    {
        DisposeDecorations();
        DisposeCutParticles();
        DisposeChunks();
        DisposeCutMask();
        DisposeReleasedBlocks();
        DisposeReleasedBlockWalls();
        DisposeReleasedBlockResources();
        DisposeCells();
        DisposeEcsQuery();
    }

    private void LateUpdate()
    {
        UpdateMetaballWaterRendering();
        DrawReleasedBlocks();

        if (_dirtyChunks.Count == 0)
            return;

        int rebuildCount = Mathf.Min(_maxChunkRebuildsPerFrame, _dirtyChunks.Count);
        _scheduledChunkRebuilds.Clear();
        for (int i = 0; i < rebuildCount; i++)
        {
            int lastIndex = _dirtyChunks.Count - 1;
            int chunkIndex = _dirtyChunks[lastIndex];
            _dirtyChunks.RemoveAt(lastIndex);
            if (_chunkDirty != null && chunkIndex >= 0 && chunkIndex < _chunkDirty.Length)
                _chunkDirty[chunkIndex] = 0;

            _scheduledChunkRebuilds.Add(chunkIndex);
        }

        ScheduleAndApplyChunkRebuilds();
    }
}
