using System.Collections.Generic;
using Crusher;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
using Collider = Unity.Physics.Collider;
using RenderMaterial = UnityEngine.Material;

[DisallowMultipleComponent]
public sealed partial class LevelMapAuthoring : MonoBehaviour
{
    private const int MaxInstancesPerBatch = 1023;
    private const float MinimumPhysicsDeltaTime = 0.001f;
    private const float MaxCellTravelPerStep = 0.75f;
    private const float MaxRadiusTravelPerStep = 0.75f;

    private static readonly List<LevelMapAuthoring> ActiveSpawners = new();
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static int _nextOwnerId = 1;

    [Header("Source")]
    [SerializeField] private GameObject[] _levelPrefabs;
    [SerializeField, Min(1)] private int _startLevel = 1;
    [SerializeField] private Transform _container;
    [SerializeField] private RenderMaterial _chunkMaterial;
    [SerializeField] private Mesh[] _terrainFragmentMeshes;
    [SerializeField] private bool _spawnOnAwake = true;

    [Header("Soil Visual")]
    [SerializeField] private bool _overrideDirtPalette = true;
    // Vertex colours are consumed as linear values by the terrain shader. These low
    // channel values display as the dark chocolate-brown used by the reference game.
    [SerializeField] private Color _soilBaseColor = new Color32(18, 10, 8, 255);

    [Header("Chunk Rendering")]
    [SerializeField, Range(8, 64)] private int _chunkSize = 24;
    [SerializeField, Range(1, 8)] private int _maxChunkRebuildsPerFrame = 2;

    [Header("Released Blocks")]
    [SerializeField, Min(0f)] private float _releasedBlockLiftSpeed = 1.5f;
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
    private readonly List<PendingReleasedBlock> _pendingReleasedBlocks = new(64);
    private readonly List<Entity> _metaballWaterEntities = new(64);
    private readonly List<Entity> _releasedBlockWallEntities = new(4);
    private readonly List<BlobAssetReference<Collider>> _releasedBlockWallColliders = new(4);
    private readonly List<ReleasedBlockRuntimeType> _releasedBlockTypes = new(4);

    private readonly RenderFrameData[] _renderFrames = new RenderFrameData[2];

    private NativeArray<Color32> _cellColors;
    private NativeArray<Color32> _cellReleasedColors;
    private NativeArray<byte> _cellSolid;
    private NativeArray<byte> _breakableCellMask;
    private byte[] _cellSolidSnapshot;
    private NativeArray<ushort> _cellReleasedTypes;
    private NativeArray<Vector3> _authoredMeshVertices;
    private NativeArray<Vector2> _authoredMeshUvs;
    private NativeArray<int> _authoredMeshIndices;
    private NativeArray<AuthoredMeshRange> _authoredMeshRanges;

    private World _ecsWorld;
    private EntityManager _entityManager;
    private EntityQuery _releasedBlockQuery;
    private EntityArchetype _releasedBlockArchetype;
    private MaterialPropertyBlock _releasedBlockPropertyBlock;
    private ParticleSystem _cutParticles;
    private RenderMaterial _cutParticleMaterial;

    private byte[] _chunkDirty;
    private Transform _runtimeParent;
    private Vector3 _offset;

    private int _ownerId;
    private int _gridWidth;
    private int _gridHeight;
    private int _chunkColumns;
    private int _currentLevel;
    private Vector2 _cellSize;
    private float _chunkColliderDepth;

    private ParticleSystem.Particle[] _metaballParticleBuffer;
    private int _spawnedWaterCellCount;

    private bool _hasReleasedBlockQuery;
    private bool _spawnRequested;
    private int _displayRenderFrame = -1;
    private int _scheduledRenderFrame = -1;
    private bool _isDestroying;

    internal struct AuthoredMeshRange
    {
        public int VertexStart;
        public int VertexCount;
        public int IndexStart;
        public int IndexCount;
    }

    private sealed class ReleasedBlockRuntimeType
    {
        public GameObject Prefab;
        public Mesh Mesh;
        public RenderMaterial Material;
        public BlobAssetReference<Collider> Collider;
        public float Scale;
        public Vector3 RenderScale;
        public float Radius;
        public TypeBlock CollectibleType;
        public Matrix4x4[][] BatchMatrices;
        public Vector4[][] BatchColors;
        public int[] BatchCounts;
        public int BatchCount;
        public int RenderCount;
    }

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
        _currentLevel = Mathf.Clamp(_startLevel, 1, Mathf.Max(1, _levelPrefabs?.Length ?? 0));
        _spawnRequested = _spawnOnAwake;
    }

    private void OnDestroy()
    {
        _isDestroying = true;
        CompleteReleasedBlockJobs();
        DisposeDecorations();
        DisposeCutParticles();
        DisposeChunks();
        DisposeReleasedBlocks();
        DisposeReleasedBlockWalls();
        DisposeReleasedBlockResources();
        DisposeCells();
        DisposeEcsQuery();
    }

    private struct PendingReleasedBlock
    {
        public Entity Entity;
        public int ChunkIndex;
    }

    internal void UpdateMap()
    {
        if (_spawnRequested)
        {
            _spawnRequested = false;
            Spawn();
        }

        if (!_cellSolid.IsCreated)
        {
            return;
        }

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
