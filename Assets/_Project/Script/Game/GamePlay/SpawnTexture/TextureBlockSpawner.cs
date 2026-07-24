using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
using Collider = Unity.Physics.Collider;
using RenderMaterial = UnityEngine.Material;

public sealed partial class TextureBlockSpawner : MonoBehaviour
{
    [SerializeField] private Texture2D _texture;
    [SerializeField] private Transform _container;
    [SerializeField] private RenderMaterial _chunkMaterial;
    [SerializeField] private float _pixelSize = 0.12f;
    [SerializeField, Range(1, 16)] private int _sampleStep = 1;
    [SerializeField, Range(0f, 1f)] private float _alphaThreshold = 0.1f;
    [SerializeField] private float _chunkColliderDepth = 0.25f;
    [SerializeField] private float _sawReleaseRadius = 0.18f;
    [SerializeField] private bool _renderVoxelDetailFromStart = true;
    [SerializeField, Range(0.75f, 1f)] private float _detailVoxelScale = 0.94f;
    [SerializeField, Range(0f, 30f)] private float _voxelRandomYRotation = 20f;
    [SerializeField, Min(0)] private int _maxPhysicsDebrisPerFrame;
    [SerializeField, Range(8, 64)] private int _chunkSize = 24;
    [SerializeField, Range(1, 8)] private int _maxChunkRebuildsPerFrame = 2;
    [SerializeField, Range(0f, 20f)] private float _releasedBlockDamping = 2.5f;
    [SerializeField, Range(0f, 20f)] private float _releasedBlockAngularDamping = 4f;
    [SerializeField, Range(0.01f, 10f)] private float _releasedBlockMass = 0.1f;
    [SerializeField, Range(128, 10000)] private int _maxReleasedPhysicsBlocks = 5000;
    [SerializeField, Range(1, 4)] private int _releasedBlockRenderInterval = 2;
    [SerializeField, Range(0f, 1f)] private float _physicsFriction = 0.12f;
    [SerializeField, Range(0f, 1f)] private float _physicsRestitution;
    [SerializeField] private ReleasedBlockAuthoring _releasedBlockAuthoring;
    [SerializeField] private Transform[] _releasedBlockWalls;
    [SerializeField] private bool _centerTexture = true;
    [SerializeField] private bool _spawnOnAwake = true;
    [Header("Metaball Water")]
    [SerializeField] private bool _spawnMetaballWater = true;
    [SerializeField, Min(0)] private int _waterClusterCount = 3;
    [SerializeField, Min(1)] private int _waterCellsPerClusterMin = 10;
    [SerializeField, Min(1)] private int _waterCellsPerClusterMax = 20;
    [SerializeField, Range(1f, 3f)] private float _waterParticleScale = 1.65f;
    [SerializeField] private ParticleSystem _metaballParticles;
    [SerializeField] private RenderMaterial _metaballSourceMaterial;

    private readonly List<ChunkRuntime> _chunks = new();
    private static readonly List<TextureBlockSpawner> ActiveSpawners = new();
    private static int _nextOwnerId = 1;

    private readonly List<int> _dirtyChunks = new(16);
    private readonly List<int> _scheduledChunkRebuilds = new(8);
    private readonly List<JobHandle> _scheduledChunkHandles = new(8);

    private readonly List<Entity> _releasedBlockEntities = new(1024);
    private readonly List<Entity> _metaballWaterEntities = new(64);
    private readonly List<Entity> _releasedBlockWallEntities = new(4);
    private readonly List<BlobAssetReference<Collider>> _releasedBlockWallColliders = new(4);

    private readonly RenderFrameData[] _renderFrames = new RenderFrameData[2];
    private Matrix4x4[][] _renderBatchMatrices;
    private Vector4[][] _renderBatchColors;
    private int[] _renderBatchCounts;
    private int _renderBatchCount;

    private NativeArray<Color32> _cellColors;
    private NativeArray<byte> _cellSolid;

    private World _ecsWorld;
    private EntityManager _entityManager;
    private EntityQuery _releasedBlockQuery;
    private EntityArchetype _releasedBlockArchetype;
    private BlobAssetReference<Collider> _releasedBlockCollider;

    private Mesh _releasedBlockMesh;
    private RenderMaterial _releasedBlockMaterial;
    private MaterialPropertyBlock _releasedBlockPropertyBlock;

    private byte[] _chunkDirty;
    private Transform _runtimeParent;
    private RenderMaterial _runtimeChunkMaterial;
    private Vector3 _offset;

    private int _ownerId;
    private int _gridWidth;
    private int _gridHeight;
    private int _chunkColumns;
    private float _cellSize;
    private float _releasedBlockScale = 1f;

    private int _physicsDebrisFrame = -1;
    private int _physicsDebrisSpawnedThisFrame;

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

    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public static int SuckedBlockCount { get; private set; }
    public static event System.Action<int> BlocksSucked;

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
        if (_spawnOnAwake)
            Spawn();
    }

    private void OnDestroy()
    {
        DisposeChunks();
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
