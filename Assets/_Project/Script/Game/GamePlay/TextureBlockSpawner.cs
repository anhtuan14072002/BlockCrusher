using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using Collider = Unity.Physics.Collider;
using PhysicsMaterial = Unity.Physics.Material;
using RenderMaterial = UnityEngine.Material;
public sealed class TextureBlockSpawner : MonoBehaviour
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
    private readonly List<ChunkRuntime> _chunks = new();
    private static readonly List<TextureBlockSpawner> ActiveSpawners = new();
    private static int _nextOwnerId = 1;
    private readonly List<int> _dirtyChunks = new(16);
    private readonly List<int> _scheduledChunkRebuilds = new(8);
    private readonly List<JobHandle> _scheduledChunkHandles = new(8);
    private readonly List<Entity> _releasedBlockEntities = new(1024);
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
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    public static int SuckedBlockCount { get; private set; }
    public static event System.Action<int> BlocksSucked;
    public static bool ReleaseAtWorldForActiveSpawners(Vector3 worldPoint, Vector3 pressDirection, float pressSpeed,
        float outwardForce, float tangentialForce, float spinDirection, float bladeRadius, float sideDamping,
        float maxVelocity)
    {
        bool releasedAny = false;
        for (int i = ActiveSpawners.Count - 1; i >= 0; i--)
        {
            TextureBlockSpawner spawner = ActiveSpawners[i];
            if (spawner == null)
            {
                ActiveSpawners.RemoveAt(i);
                continue;
            }
            releasedAny |= spawner.ReleaseAtWorld(worldPoint, pressDirection, pressSpeed, outwardForce, tangentialForce,
                spinDirection, bladeRadius, sideDamping, maxVelocity);
        }
        return releasedAny;
    }
    public static bool HasSolidAtWorldForActiveSpawners(Vector3 worldPoint, float radius)
    {
        for (int i = ActiveSpawners.Count - 1; i >= 0; i--)
        {
            TextureBlockSpawner spawner = ActiveSpawners[i];
            if (spawner == null)
            {
                ActiveSpawners.RemoveAt(i);
                continue;
            }
            if (spawner.HasSolidAtWorld(worldPoint, radius))
                return true;
        }
        return false;
    }
    public static void ApplyConveyorForActiveSpawners(Bounds bounds, Vector3 direction, float speed, float acceleration,
        float deltaTime)
    {
        for (int i = 0; i < ActiveSpawners.Count; i++)
            ActiveSpawners[i].ApplyConveyor(bounds, direction, speed, acceleration, deltaTime);
    }
    public static void ClearReleasedBlocksForActiveSpawners(Bounds bounds)
    {
        for (int i = 0; i < ActiveSpawners.Count; i++)
            ActiveSpawners[i].DespawnInBounds(bounds);
    }
    public static void ApplySuctionForActiveSpawners(Vector3 origin, Quaternion rotation, Vector3 boxSize,
        float force, float acceleration, float maxVelocity, float arrivalDamping, float destroyRadius, float deltaTime)
    {
        for (int i = 0; i < ActiveSpawners.Count; i++)
            ActiveSpawners[i].ApplySuction(origin, rotation, boxSize, force, acceleration, maxVelocity, arrivalDamping,
                destroyRadius, deltaTime);
    }
    public static void ScaleSawReleaseRadiusForActiveSpawners(float multiplier)
    {
        for (int i = 0; i < ActiveSpawners.Count; i++)
            ActiveSpawners[i]._sawReleaseRadius *= multiplier;
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
        ApplyPendingSawPush();
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
    [ContextMenu("Spawn")]
    public void Spawn()
    {
        if (_texture == null)
            return;
        Clear();
        Color32[] pixels;
        try
        {
            pixels = _texture.GetPixels32();
        }
        catch (UnityException)
        {
            Debug.LogError("TextureBlockSpawner needs a readable texture.", this);
            return;
        }
        int width = _texture.width;
        int height = _texture.height;
        _runtimeParent = _container != null ? _container : transform;
        _cellSize = _pixelSize * _sampleStep;
        _gridWidth = Mathf.CeilToInt(width / (float)_sampleStep);
        _gridHeight = Mathf.CeilToInt(height / (float)_sampleStep);
        _offset = _centerTexture
            ? new Vector3((_gridWidth - 1) * _cellSize * -0.5f, (_gridHeight - 1) * _cellSize * -0.5f, 0f)
            : Vector3.zero;
        DisposeCells();
        _cellColors = new NativeArray<Color32>(_gridWidth * _gridHeight, Allocator.Persistent);
        _cellSolid = new NativeArray<byte>(_gridWidth * _gridHeight, Allocator.Persistent);
        NativeArray<Color32> texturePixels = new NativeArray<Color32>(pixels, Allocator.TempJob);
        TextureToCellsJob textureJob = new TextureToCellsJob
        {
            TexturePixels = texturePixels,
            CellColors = _cellColors,
            CellSolid = _cellSolid,
            TextureWidth = width,
            TextureHeight = height,
            GridWidth = _gridWidth,
            SampleStep = _sampleStep,
            AlphaLimit = (byte)Mathf.RoundToInt(_alphaThreshold * 255f)
        };
        textureJob.Schedule(_cellColors.Length, 64).Complete();
        texturePixels.Dispose();
        _runtimeChunkMaterial = ResolveChunkMaterial();
        CreateChunks();
    }
    [ContextMenu("Clear")]
    public void Clear()
    {
        DisposeCells();
        DisposeChunks();
        _chunks.Clear();
        _dirtyChunks.Clear();
        _scheduledChunkRebuilds.Clear();
        _scheduledChunkHandles.Clear();
        _chunkDirty = null;
        ClearReleasedBlockEntities();
        Transform parent = _container != null ? _container : transform;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }
    public bool ReleaseAtWorld(Vector3 worldPoint, Vector3 pressDirection, float pressSpeed, float outwardForce,
        float tangentialForce, float spinDirection, float bladeRadius, float sideDamping, float maxVelocity)
    {
        if (!_cellSolid.IsCreated || _runtimeParent == null)
            return false;
        QueueSawPush(worldPoint, pressDirection, pressSpeed, outwardForce, tangentialForce, spinDirection,
            bladeRadius, maxVelocity);
        Vector3 localPoint = _runtimeParent.InverseTransformPoint(worldPoint);
        float radiusSqr = _sawReleaseRadius * _sawReleaseRadius;
        int centerX = Mathf.RoundToInt((localPoint.x - _offset.x) / _cellSize);
        int centerY = Mathf.RoundToInt((localPoint.y - _offset.y) / _cellSize);
        int radiusCells = Mathf.Max(1, Mathf.CeilToInt(_sawReleaseRadius / _cellSize));
        int minX = Mathf.Max(0, centerX - radiusCells);
        int maxX = Mathf.Min(_gridWidth - 1, centerX + radiusCells);
        int minY = Mathf.Max(0, centerY - radiusCells);
        int maxY = Mathf.Min(_gridHeight - 1, centerY + radiusCells);
        bool releasedAny = false;
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector3 cellLocal = GetCellLocalPosition(x, y);
                Vector3 delta = cellLocal - localPoint;
                if (delta.x * delta.x + delta.y * delta.y > radiusSqr)
                    continue;
                int cellIndex = y * _gridWidth + x;
                if (_cellSolid[cellIndex] == 0)
                    continue;
                Color32 color = _cellColors[cellIndex];
                _cellSolid[cellIndex] = 0;
                if (TryConsumePhysicsDebrisBudget())
                {
                    QueueReleasedBlockSpawn(cellLocal, color, worldPoint, pressDirection, pressSpeed, outwardForce,
                        tangentialForce, spinDirection, bladeRadius, sideDamping, maxVelocity);
                }
                MarkCellChunkDirty(x, y);
                releasedAny = true;
            }
        }
        return releasedAny;
    }
    public bool HasSolidAtWorld(Vector3 worldPoint, float radius)
    {
        if (!_cellSolid.IsCreated || _runtimeParent == null)
            return false;
        Vector3 localPoint = _runtimeParent.InverseTransformPoint(worldPoint);
        float radiusSqr = radius * radius;
        int centerX = Mathf.RoundToInt((localPoint.x - _offset.x) / _cellSize);
        int centerY = Mathf.RoundToInt((localPoint.y - _offset.y) / _cellSize);
        int radiusCells = Mathf.Max(1, Mathf.CeilToInt(radius / _cellSize));
        int minX = Mathf.Max(0, centerX - radiusCells);
        int maxX = Mathf.Min(_gridWidth - 1, centerX + radiusCells);
        int minY = Mathf.Max(0, centerY - radiusCells);
        int maxY = Mathf.Min(_gridHeight - 1, centerY + radiusCells);
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int cellIndex = y * _gridWidth + x;
                if (_cellSolid[cellIndex] == 0)
                    continue;
                Vector3 cellLocal = GetCellLocalPosition(x, y);
                Vector3 delta = cellLocal - localPoint;
                if (delta.x * delta.x + delta.y * delta.y <= radiusSqr)
                    return true;
            }
        }
        return false;
    }
    private void MarkCellChunkDirty(int cellX, int cellY)
    {
        if (_chunks.Count == 0 || _chunkDirty == null || _chunkColumns <= 0)
            return;
        int chunkX = cellX / _chunkSize;
        int chunkY = cellY / _chunkSize;
        int chunkIndex = chunkY * _chunkColumns + chunkX;
        if ((uint)chunkIndex >= (uint)_chunks.Count || _chunkDirty[chunkIndex] != 0)
            return;
        _chunkDirty[chunkIndex] = 1;
        _dirtyChunks.Add(chunkIndex);
    }
    private bool TryConsumePhysicsDebrisBudget()
    {
        if (_maxPhysicsDebrisPerFrame == 0)
            return true;
        int frame = Time.frameCount;
        if (_physicsDebrisFrame != frame)
        {
            _physicsDebrisFrame = frame;
            _physicsDebrisSpawnedThisFrame = 0;
        }
        if (_physicsDebrisSpawnedThisFrame >= _maxPhysicsDebrisPerFrame)
            return false;
        _physicsDebrisSpawnedThisFrame++;
        return true;
    }
    private void CreateChunks()
    {
        RenderMaterial material = _runtimeChunkMaterial;
        _chunkColumns = Mathf.CeilToInt(_gridWidth / (float)_chunkSize);
        int chunkRows = Mathf.CeilToInt(_gridHeight / (float)_chunkSize);
        int chunkCount = _chunkColumns * chunkRows;
        _chunkDirty = new byte[chunkCount];
        for (int chunkY = 0; chunkY < chunkRows; chunkY++)
        {
            for (int chunkX = 0; chunkX < _chunkColumns; chunkX++)
            {
                int startX = chunkX * _chunkSize;
                int startY = chunkY * _chunkSize;
                int width = Mathf.Min(_chunkSize, _gridWidth - startX);
                int height = Mathf.Min(_chunkSize, _gridHeight - startY);
                GameObject chunkObject = new GameObject("TextureChunk_" + chunkX + "_" + chunkY);
                chunkObject.transform.SetParent(_runtimeParent, false);
                MeshFilter meshFilter = chunkObject.AddComponent<MeshFilter>();
                MeshRenderer meshRenderer = chunkObject.AddComponent<MeshRenderer>();
                TextureBlockChunk chunk = chunkObject.AddComponent<TextureBlockChunk>();
                Mesh mesh = new Mesh { name = "TextureChunk_Mesh_" + chunkX + "_" + chunkY };
                mesh.MarkDynamic();
                meshFilter.sharedMesh = mesh;
                meshRenderer.sharedMaterial = material;
                chunk.Initialize(this);
                _chunks.Add(CreateChunkRuntime(mesh, meshRenderer, startX, startY, width, height,
                    _renderVoxelDetailFromStart));
            }
        }
        _scheduledChunkRebuilds.Clear();
        for (int i = 0; i < _chunks.Count; i++)
            _scheduledChunkRebuilds.Add(i);
        ScheduleAndApplyChunkRebuilds();
    }
    private ChunkRuntime CreateChunkRuntime(Mesh mesh, MeshRenderer renderer, int startX, int startY, int width,
        int height, bool isDetailed)
    {
        int maxCells = width * height;
        int vertexCapacity = isDetailed ? maxCells * 24 : maxCells * 4;
        int indexCapacity = isDetailed ? maxCells * 36 : maxCells * 6;
        return new ChunkRuntime
        {
            Mesh = mesh,
            Renderer = renderer,
            StartX = startX,
            StartY = startY,
            Width = width,
            Height = height,
            IsDetailed = isDetailed,
            Visited = new NativeArray<byte>(maxCells, Allocator.Persistent),
            Vertices = new NativeList<Vector3>(vertexCapacity, Allocator.Persistent),
            Colors = new NativeList<Color32>(vertexCapacity, Allocator.Persistent),
            Uvs = new NativeList<Vector2>(vertexCapacity, Allocator.Persistent),
            Indices = new NativeList<int>(indexCapacity, Allocator.Persistent),
            ColliderVisited = new NativeArray<byte>(maxCells, Allocator.Persistent),
            ColliderVertices = new NativeList<Vector3>(maxCells * 24, Allocator.Persistent),
            ColliderColors = new NativeList<Color32>(maxCells * 24, Allocator.Persistent),
            ColliderUvs = new NativeList<Vector2>(maxCells * 24, Allocator.Persistent),
            ColliderIndices = new NativeList<int>(maxCells * 36, Allocator.Persistent)
        };
    }
    private void ScheduleAndApplyChunkRebuilds()
    {
        if (_scheduledChunkRebuilds.Count == 0)
            return;
        _scheduledChunkHandles.Clear();
        for (int i = 0; i < _scheduledChunkRebuilds.Count; i++)
        {
            int chunkIndex = _scheduledChunkRebuilds[i];
            if ((uint)chunkIndex >= (uint)_chunks.Count)
                continue;
            ChunkRuntime chunk = _chunks[chunkIndex];
            chunk.Vertices.Clear();
            chunk.Colors.Clear();
            chunk.Uvs.Clear();
            chunk.Indices.Clear();
            chunk.ColliderVertices.Clear();
            chunk.ColliderColors.Clear();
            chunk.ColliderUvs.Clear();
            chunk.ColliderIndices.Clear();
            BuildChunkMeshJob meshJob = new BuildChunkMeshJob
            {
                CellColors = _cellColors,
                CellSolid = _cellSolid,
                Visited = chunk.Visited,
                Vertices = chunk.Vertices,
                Colors = chunk.Colors,
                Uvs = chunk.Uvs,
                Indices = chunk.Indices,
                GridWidth = _gridWidth,
                StartX = chunk.StartX,
                StartY = chunk.StartY,
                ChunkWidth = chunk.Width,
                ChunkHeight = chunk.Height,
                CellSize = _cellSize,
                Offset = _offset,
                UseVoxelDetail = chunk.IsDetailed ? (byte)1 : (byte)0,
                ExtrudeMergedQuads = 0,
                MergeAnySolid = 0,
                DetailVoxelScale = _detailVoxelScale,
                RandomYRotation = _voxelRandomYRotation,
                DetailDepth = _chunkColliderDepth
            };
            _scheduledChunkHandles.Add(meshJob.Schedule());
            BuildChunkMeshJob colliderJob = new BuildChunkMeshJob
            {
                CellColors = _cellColors,
                CellSolid = _cellSolid,
                Visited = chunk.ColliderVisited,
                Vertices = chunk.ColliderVertices,
                Colors = chunk.ColliderColors,
                Uvs = chunk.ColliderUvs,
                Indices = chunk.ColliderIndices,
                GridWidth = _gridWidth,
                StartX = chunk.StartX,
                StartY = chunk.StartY,
                ChunkWidth = chunk.Width,
                ChunkHeight = chunk.Height,
                CellSize = _cellSize,
                Offset = _offset,
                UseVoxelDetail = 0,
                ExtrudeMergedQuads = 1,
                MergeAnySolid = 1,
                DetailVoxelScale = _detailVoxelScale,
                RandomYRotation = 0f,
                DetailDepth = _chunkColliderDepth
            };
            _scheduledChunkHandles.Add(colliderJob.Schedule());
        }
        JobHandle combinedHandle = default;
        for (int i = 0; i < _scheduledChunkHandles.Count; i++)
            combinedHandle = JobHandle.CombineDependencies(combinedHandle, _scheduledChunkHandles[i]);
        combinedHandle.Complete();
        for (int i = 0; i < _scheduledChunkRebuilds.Count; i++)
        {
            int chunkIndex = _scheduledChunkRebuilds[i];
            if ((uint)chunkIndex >= (uint)_chunks.Count)
                continue;
            ChunkRuntime chunk = _chunks[chunkIndex];
            ApplyChunkMesh(ref chunk);
            _chunks[chunkIndex] = chunk;
        }
    }
    private void ApplyChunkMesh(ref ChunkRuntime chunk)
    {
        Mesh mesh = chunk.Mesh;
        mesh.Clear();
        if (chunk.Vertices.Length > 0)
        {
            mesh.indexFormat = chunk.Vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(chunk.Vertices.AsArray());
            mesh.SetColors(chunk.Colors.AsArray());
            mesh.SetUVs(0, chunk.Uvs.AsArray());
            mesh.SetIndices(chunk.Indices.AsArray(), MeshTopology.Triangles, 0);
            if (chunk.IsDetailed)
                mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            chunk.Renderer.enabled = true;
            ApplyChunkPhysicsCollider(ref chunk);
        }
        else
        {
            chunk.Renderer.enabled = false;
            DestroyChunkPhysicsCollider(ref chunk);
        }
    }
    private void ApplyChunkPhysicsCollider(ref ChunkRuntime chunk)
    {
        if (!EnsureEcsReady())
            return;
        if (chunk.ColliderVertices.Length == 0 || chunk.ColliderIndices.Length == 0)
        {
            DestroyChunkPhysicsCollider(ref chunk);
            return;
        }
        int triangleCount = chunk.ColliderIndices.Length / 3;
        NativeArray<float3> vertices = new NativeArray<float3>(chunk.ColliderVertices.Length, Allocator.Temp);
        NativeArray<int3> triangles = new NativeArray<int3>(triangleCount, Allocator.Temp);
        for (int i = 0; i < chunk.ColliderVertices.Length; i++)
        {
            Vector3 world = _runtimeParent.TransformPoint(chunk.ColliderVertices[i]);
            vertices[i] = new float3(world.x, world.y, world.z);
        }
        for (int i = 0; i < triangleCount; i++)
        {
            int index = i * 3;
            triangles[i] = new int3(chunk.ColliderIndices[index], chunk.ColliderIndices[index + 1],
                chunk.ColliderIndices[index + 2]);
        }
        PhysicsMaterial physicsMaterial = CreatePhysicsMaterial();
        BlobAssetReference<Collider> collider = Unity.Physics.MeshCollider.Create(vertices, triangles,
            CollisionFilter.Default, physicsMaterial);
        vertices.Dispose();
        triangles.Dispose();
        if (chunk.PhysicsCollider.IsCreated)
            chunk.PhysicsCollider.Dispose();
        chunk.PhysicsCollider = collider;
        if (chunk.PhysicsEntity == Entity.Null || !_entityManager.Exists(chunk.PhysicsEntity))
        {
            chunk.PhysicsEntity = _entityManager.CreateEntity(typeof(LocalTransform), typeof(PhysicsCollider));
            _entityManager.SetComponentData(chunk.PhysicsEntity,
                LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));
            _entityManager.AddSharedComponent(chunk.PhysicsEntity, new PhysicsWorldIndex(0));
        }
        _entityManager.SetComponentData(chunk.PhysicsEntity, new PhysicsCollider { Value = chunk.PhysicsCollider });
    }
    private void DestroyChunkPhysicsCollider(ref ChunkRuntime chunk)
    {
        if (EnsureEcsReady() && chunk.PhysicsEntity != Entity.Null && _entityManager.Exists(chunk.PhysicsEntity))
            _entityManager.DestroyEntity(chunk.PhysicsEntity);
        chunk.PhysicsEntity = Entity.Null;
        if (chunk.PhysicsCollider.IsCreated)
        {
            chunk.PhysicsCollider.Dispose();
            chunk.PhysicsCollider = default;
        }
    }
    private void DisposeChunks()
    {
        for (int i = 0; i < _chunks.Count; i++)
        {
            ChunkRuntime chunk = _chunks[i];
            if (chunk.Visited.IsCreated)
                chunk.Visited.Dispose();
            if (chunk.Vertices.IsCreated)
                chunk.Vertices.Dispose();
            if (chunk.Colors.IsCreated)
                chunk.Colors.Dispose();
            if (chunk.Uvs.IsCreated)
                chunk.Uvs.Dispose();
            if (chunk.Indices.IsCreated)
                chunk.Indices.Dispose();
            if (chunk.ColliderVisited.IsCreated)
                chunk.ColliderVisited.Dispose();
            if (chunk.ColliderVertices.IsCreated)
                chunk.ColliderVertices.Dispose();
            if (chunk.ColliderColors.IsCreated)
                chunk.ColliderColors.Dispose();
            if (chunk.ColliderUvs.IsCreated)
                chunk.ColliderUvs.Dispose();
            if (chunk.ColliderIndices.IsCreated)
                chunk.ColliderIndices.Dispose();
            DestroyChunkPhysicsCollider(ref chunk);
            if (chunk.Mesh != null)
                DestroyUnityObject(chunk.Mesh);
        }
        _chunks.Clear();
    }
    private RenderMaterial ResolveChunkMaterial()
    {
        if (_chunkMaterial != null)
            return _chunkMaterial;
        Shader voxelShader = Shader.Find("BlockCrusher/VoxelExactColor");
        if (voxelShader != null)
            return new RenderMaterial(voxelShader);
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
            return new RenderMaterial(shader);
        return null;
    }
    private Vector3 GetCellLocalPosition(int x, int y)
    {
        return _offset + new Vector3(x * _cellSize, y * _cellSize, 0f);
    }
    private void QueueReleasedBlockSpawn(Vector3 localPosition, Color32 color, Vector3 sawCenter,
        Vector3 pressDirection,
        float pressSpeed, float outwardForce, float tangentialForce, float spinDirection, float bladeRadius,
        float sideDamping, float maxVelocity)
    {
        if (!EnsureReleasedBlockResources() || !EnsureEcsReady())
            return;
        Vector3 position = _runtimeParent.TransformPoint(localPosition);
        Vector3 outward = position - sawCenter;
        outward.z = 0f;
        float distance = outward.magnitude;
        if (distance <= 0.0001f)
            outward = pressDirection.sqrMagnitude > 0.0001f ? pressDirection : Vector3.up;
        else
            outward.Normalize();
        float radiusPush = bladeRadius > 0f ? Mathf.Clamp01((bladeRadius - distance) / bladeRadius) : 0f;
        Vector3 tangent = new Vector3(-outward.y, outward.x, 0f) * Mathf.Sign(spinDirection);
        float speedScale = 1f + Mathf.Min(pressSpeed, 4f) * 0.1f;
        float pushScale = 1f + radiusPush * 1.25f;
        Vector3 velocity = outward * (outwardForce * 0.08f * pushScale * speedScale) +
                           (Vector3.up + tangent * 0.1f).normalized *
                           (tangentialForce * 0.21f * pushScale * speedScale * radiusPush);
        velocity -= outward * Vector3.Dot(velocity, outward) * sideDamping * radiusPush * 0.15f;
        float safeMaxVelocity = GetSafePhysicsVelocity(maxVelocity);
        Vector3 clampedVelocity = Vector3.ClampMagnitude(velocity, safeMaxVelocity);
        clampedVelocity = RedirectVelocityFromSolid(position, clampedVelocity);
        if (_releasedBlockEntities.Count >= _maxReleasedPhysicsBlocks)
            TrimReleasedBlockEntityList();
        while (_releasedBlockEntities.Count >= _maxReleasedPhysicsBlocks && _releasedBlockEntities.Count > 0)
            DestroyReleasedBlockEntityAt(0);
        Entity entity = _entityManager.CreateEntity(_releasedBlockArchetype);
        _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(
            new float3(position.x, position.y, position.z), quaternion.identity, _releasedBlockScale));
        _entityManager.SetComponentData(entity, new PhysicsCollider { Value = _releasedBlockCollider });
        PhysicsMass physicsMass = PhysicsMass.CreateDynamic(
            _releasedBlockCollider.Value.MassProperties, _releasedBlockMass);
        physicsMass.InverseInertia.x = 0f;
        physicsMass.InverseInertia.y = 0f;
        _entityManager.SetComponentData(entity, physicsMass);
        _entityManager.SetComponentData(entity, new PhysicsVelocity
        {
            Linear = new float3(clampedVelocity.x, clampedVelocity.y, clampedVelocity.z),
            Angular = float3.zero
        });
        _entityManager.SetComponentData(entity, new PhysicsDamping
        {
            Linear = _releasedBlockDamping,
            Angular = _releasedBlockAngularDamping
        });
        _entityManager.SetComponentData(entity, new PhysicsGravityFactor { Value = 1f });
        _entityManager.SetComponentData(entity, new ReleasedBlockComponent
        {
            OwnerId = _ownerId,
            Color = ToFloat4(color),
            LockedZ = position.z,
            MaxPlanarSpeed = safeMaxVelocity
        });
        _releasedBlockEntities.Add(entity);
    }
    private bool EnsureReleasedBlockResources()
    {
        if (_releasedBlockCollider.IsCreated && _releasedBlockMesh != null && _releasedBlockMaterial != null)
        {
            EnsureRenderFrameResources();
            return true;
        }
        ReleasedBlockAuthoring authoring = ResolveReleasedBlockAuthoring();
        GameObject prefab = authoring != null ? authoring.ReleasedBlockPrefab : null;
        if (prefab == null)
            return false;
        MeshFilter meshFilter = prefab.GetComponentInChildren<MeshFilter>();
        Renderer meshRenderer = prefab.GetComponentInChildren<Renderer>();
        if (meshFilter == null || meshFilter.sharedMesh == null || meshRenderer == null)
            return false;
        _releasedBlockMesh = meshFilter.sharedMesh;
        _releasedBlockMaterial = new RenderMaterial(meshRenderer.sharedMaterial != null
            ? meshRenderer.sharedMaterial
            : _runtimeChunkMaterial)
        {
            enableInstancing = true
        };
        _releasedBlockPropertyBlock ??= new MaterialPropertyBlock();
        _releasedBlockScale = Mathf.Max(0.0001f, authoring.Scale);
        Vector3 size = Vector3.one;
        Vector3 center = Vector3.zero;
        UnityEngine.BoxCollider box = prefab.GetComponentInChildren<UnityEngine.BoxCollider>();
        if (box != null)
        {
            size = Vector3.Scale(box.size, box.transform.lossyScale);
            center = Vector3.Scale(box.center, box.transform.lossyScale);
        }
        else
        {
            Bounds bounds = _releasedBlockMesh.bounds;
            size = bounds.size;
            center = bounds.center;
        }
        PhysicsMaterial physicsMaterial = CreatePhysicsMaterial();
        float radius = Mathf.Min(size.x, size.y) * 0.48f;
        _releasedBlockCollider = Unity.Physics.SphereCollider.Create(new SphereGeometry
        {
            Center = new float3(center.x, center.y, center.z),
            Radius = radius
        }, CollisionFilter.Default, physicsMaterial);
        EnsureRenderFrameResources();
        return _releasedBlockCollider.IsCreated;
    }
    private ReleasedBlockAuthoring ResolveReleasedBlockAuthoring()
    {
        if (_releasedBlockAuthoring == null)
            _releasedBlockAuthoring = GetComponent<ReleasedBlockAuthoring>();
        return _releasedBlockAuthoring;
    }
    private bool EnsureEcsReady()
    {
        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated)
            return false;
        if (_ecsWorld != world || !_hasReleasedBlockQuery)
        {
            DisposeReleasedBlockWalls();
            DisposeEcsQuery();
            _ecsWorld = world;
            _entityManager = world.EntityManager;
            _releasedBlockArchetype = _entityManager.CreateArchetype(
                typeof(LocalTransform), typeof(PhysicsCollider), typeof(PhysicsMass), typeof(PhysicsVelocity),
                typeof(PhysicsDamping), typeof(PhysicsGravityFactor), typeof(Simulate),
                typeof(ReleasedBlockComponent), typeof(PhysicsWorldIndex));
            _releasedBlockQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<ReleasedBlockComponent>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadWrite<PhysicsVelocity>());
            _hasReleasedBlockQuery = true;
            EnsurePhysicsStep();
            EnsureReleasedBlockWalls();
        }
        return true;
    }
    private void EnsurePhysicsStep()
    {
        EntityQuery query = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<PhysicsStep>());
        if (query.IsEmptyIgnoreFilter)
        {
            PhysicsStep step = PhysicsStep.Default;
            step.SubstepCount = 2;
            step.SolverIterationCount = 8;
            step.CollisionTolerance = Mathf.Max(step.CollisionTolerance, _cellSize * 0.1f);
            _entityManager.CreateSingleton(step, "Gameplay Physics Step");
        }
        else
        {
            Entity stepEntity = query.GetSingletonEntity();
            PhysicsStep step = _entityManager.GetComponentData<PhysicsStep>(stepEntity);
            int substepCount = Mathf.Max(step.SubstepCount, 2);
            int solverIterationCount = Mathf.Max(step.SolverIterationCount, 8);
            float collisionTolerance = Mathf.Max(step.CollisionTolerance, _cellSize * 0.1f);
            if (step.SubstepCount != substepCount || step.SolverIterationCount != solverIterationCount ||
                !Mathf.Approximately(step.CollisionTolerance, collisionTolerance))
            {
                step.SubstepCount = substepCount;
                step.SolverIterationCount = solverIterationCount;
                step.CollisionTolerance = collisionTolerance;
                _entityManager.SetComponentData(stepEntity, step);
            }
        }
        query.Dispose();
    }
    private void EnsureReleasedBlockWalls()
    {
        if (_releasedBlockWallEntities.Count > 0 || _releasedBlockWalls == null)
            return;
        PhysicsMaterial material = CreatePhysicsMaterial();
        for (int i = 0; i < _releasedBlockWalls.Length; i++)
        {
            Transform wall = _releasedBlockWalls[i];
            if (wall == null || !wall.gameObject.activeInHierarchy)
                continue;
            Vector3 scale = wall.lossyScale;
            Vector3 size = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            BlobAssetReference<Collider> collider = Unity.Physics.BoxCollider.Create(new BoxGeometry
            {
                Center = float3.zero,
                Size = new float3(size.x, size.y, size.z),
                Orientation = quaternion.identity,
                BevelRadius = 0f
            }, CollisionFilter.Default, material);
            Vector3 center = wall.position;
            Quaternion rotation = wall.rotation;
            Entity entity = _entityManager.CreateEntity(typeof(LocalTransform), typeof(PhysicsCollider));
            _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(
                new float3(center.x, center.y, center.z),
                new quaternion(rotation.x, rotation.y, rotation.z, rotation.w), 1f));
            _entityManager.SetComponentData(entity, new PhysicsCollider { Value = collider });
            _entityManager.AddSharedComponent(entity, new PhysicsWorldIndex(0));
            _releasedBlockWallColliders.Add(collider);
            _releasedBlockWallEntities.Add(entity);
        }
    }
    private void PushReleasedBlocks(Vector3 sawCenter, Vector3 pressDirection, float pressSpeed, float outwardForce,
        float tangentialForce, float spinDirection, float bladeRadius, float maxVelocity)
    {
        if (bladeRadius <= 0f || !EnsureEcsReady())
            return;
        using NativeArray<Entity> entities = _releasedBlockQuery.ToEntityArray(Allocator.Temp);
        using NativeArray<LocalTransform> transforms =
            _releasedBlockQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        using NativeArray<ReleasedBlockComponent> blocks =
            _releasedBlockQuery.ToComponentDataArray<ReleasedBlockComponent>(Allocator.Temp);
        float radiusSqr = bladeRadius * bladeRadius;
        for (int i = 0; i < entities.Length; i++)
        {
            if (blocks[i].OwnerId != _ownerId)
                continue;
            float3 entityPosition = transforms[i].Position;
            Vector3 delta = new Vector3(entityPosition.x - sawCenter.x, entityPosition.y - sawCenter.y, 0f);
            float distanceSqr = delta.sqrMagnitude;
            if (distanceSqr > radiusSqr)
                continue;
            float distance = Mathf.Sqrt(distanceSqr);
            Vector3 outward = distance > 0.0001f
                ? delta / distance
                : pressDirection.sqrMagnitude > 0.0001f
                    ? pressDirection
                    : Vector3.up;
            float radiusPush = 1f - Mathf.Clamp01(distance / bladeRadius);
            Vector3 tangent = new Vector3(-outward.y, outward.x, 0f) * Mathf.Sign(spinDirection);
            float speedScale = 1f + Mathf.Min(pressSpeed, 4f) * 0.1f;
            Vector3 impulseVelocity =
                outward * (outwardForce * 0.08f * speedScale * radiusPush) +
                tangent * (tangentialForce * 0.22f * speedScale * radiusPush);
            PhysicsVelocity velocity = _entityManager.GetComponentData<PhysicsVelocity>(entities[i]);
            Vector3 linear = new Vector3(velocity.Linear.x, velocity.Linear.y, 0f) + impulseVelocity;
            linear = Vector3.ClampMagnitude(linear, GetSafePhysicsVelocity(maxVelocity));
            linear = RedirectVelocityFromSolid(
                new Vector3(entityPosition.x, entityPosition.y, entityPosition.z), linear);
            velocity.Linear = new float3(linear.x, linear.y, 0f);
            _entityManager.SetComponentData(entities[i], velocity);
        }
    }
    private float GetSafePhysicsVelocity(float requestedMaxVelocity)
    {
        float fixedDeltaTime = Mathf.Max(Time.fixedDeltaTime, 0.001f);
        float maxCellTravelVelocity = _cellSize * 0.75f / fixedDeltaTime;
        return Mathf.Min(requestedMaxVelocity, maxCellTravelVelocity);
    }
    private Vector3 RedirectVelocityFromSolid(Vector3 worldPosition, Vector3 velocity)
    {
        velocity.z = 0f;
        float speed = velocity.magnitude;
        if (speed <= 0.0001f)
            return velocity;
        Vector3 direction = velocity / speed;
        float nearProbeDistance = _cellSize * 0.9f;
        float farProbeDistance = _cellSize * 1.6f;
        if (!IsSolidAlongDirection(worldPosition, direction, nearProbeDistance, farProbeDistance))
            return velocity;
        Vector3 tangent = new Vector3(-direction.y, direction.x, 0f);
        Vector3 bestDirection = Vector3.zero;
        float bestAlignment = float.MinValue;
        EvaluateFreeDirection(worldPosition, tangent, direction, nearProbeDistance, farProbeDistance,
            ref bestDirection, ref bestAlignment);
        EvaluateFreeDirection(worldPosition, -tangent, direction, nearProbeDistance, farProbeDistance,
            ref bestDirection, ref bestAlignment);
        EvaluateFreeDirection(worldPosition, Vector3.up, direction, nearProbeDistance, farProbeDistance,
            ref bestDirection, ref bestAlignment);
        EvaluateFreeDirection(worldPosition, Vector3.down, direction, nearProbeDistance, farProbeDistance,
            ref bestDirection, ref bestAlignment);
        EvaluateFreeDirection(worldPosition, Vector3.left, direction, nearProbeDistance, farProbeDistance,
            ref bestDirection, ref bestAlignment);
        EvaluateFreeDirection(worldPosition, Vector3.right, direction, nearProbeDistance, farProbeDistance,
            ref bestDirection, ref bestAlignment);
        return bestDirection.sqrMagnitude > 0f ? bestDirection * speed : Vector3.zero;
    }
    private void EvaluateFreeDirection(Vector3 worldPosition, Vector3 candidate, Vector3 desired,
        float nearProbeDistance, float farProbeDistance, ref Vector3 bestDirection, ref float bestAlignment)
    {
        candidate.z = 0f;
        candidate.Normalize();
        if (IsSolidAlongDirection(worldPosition, candidate, nearProbeDistance, farProbeDistance))
            return;
        float alignment = Vector3.Dot(candidate, desired);
        if (alignment <= bestAlignment)
            return;
        bestAlignment = alignment;
        bestDirection = candidate;
    }
    private bool IsSolidAlongDirection(Vector3 worldPosition, Vector3 direction,
        float nearProbeDistance, float farProbeDistance)
    {
        return IsSolidAtWorldCell(worldPosition + direction * nearProbeDistance) ||
               IsSolidAtWorldCell(worldPosition + direction * farProbeDistance);
    }
    private bool IsSolidAtWorldCell(Vector3 worldPosition)
    {
        if (!_cellSolid.IsCreated || _runtimeParent == null)
            return false;
        Vector3 local = _runtimeParent.InverseTransformPoint(worldPosition);
        int x = Mathf.RoundToInt((local.x - _offset.x) / _cellSize);
        int y = Mathf.RoundToInt((local.y - _offset.y) / _cellSize);
        if ((uint)x >= (uint)_gridWidth || (uint)y >= (uint)_gridHeight)
            return false;
        return _cellSolid[y * _gridWidth + x] != 0;
    }
    private void QueueSawPush(Vector3 sawCenter, Vector3 pressDirection, float pressSpeed, float outwardForce,
        float tangentialForce, float spinDirection, float bladeRadius, float maxVelocity)
    {
        _hasPendingSawPush = true;
        _pendingSawCenter = sawCenter;
        _pendingSawDirection = pressDirection;
        _pendingSawSpeed = pressSpeed;
        _pendingSawOutwardForce = outwardForce;
        _pendingSawTangentialForce = tangentialForce;
        _pendingSawSpinDirection = spinDirection;
        _pendingSawRadius = bladeRadius;
        _pendingSawMaxVelocity = maxVelocity;
    }
    private void ApplyPendingSawPush()
    {
        if (!_hasPendingSawPush)
            return;
        _hasPendingSawPush = false;
        PushReleasedBlocks(_pendingSawCenter, _pendingSawDirection, _pendingSawSpeed, _pendingSawOutwardForce,
            _pendingSawTangentialForce, _pendingSawSpinDirection, _pendingSawRadius, _pendingSawMaxVelocity);
    }
    private void DrawReleasedBlocks()
    {
        if (!EnsureEcsReady() || !EnsureReleasedBlockResources())
            return;
        ConsumePreparedRenderFrame();
        DrawCachedRenderFrame();
        if (_displayRenderFrame < 0 || Time.frameCount % _releasedBlockRenderInterval == 0)
            ScheduleRenderPreparation();
    }
    private void ConsumePreparedRenderFrame()
    {
        for (int i = 0; i < _renderFrames.Length; i++)
        {
            RenderFrameData frame = _renderFrames[i];
            if (frame == null || !frame.Pending || !frame.Handle.IsCompleted)
                continue;
            frame.Handle.Complete();
            frame.Pending = false;
            _displayRenderFrame = i;
            CachePreparedRenderFrame(frame);
        }
    }
    private void ScheduleRenderPreparation()
    {
        int writeIndex = _displayRenderFrame == 0 ? 1 : 0;
        RenderFrameData frame = _renderFrames[writeIndex];
        if (frame == null || frame.Pending)
            return;
        NativeArray<LocalTransform> transforms =
            _releasedBlockQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        NativeArray<ReleasedBlockComponent> blocks =
            _releasedBlockQuery.ToComponentDataArray<ReleasedBlockComponent>(Allocator.TempJob);
        frame.Count.Value = 0;
        JobHandle prepareHandle = new PrepareRenderFrameJob
        {
            Transforms = transforms,
            Blocks = blocks,
            Matrices = frame.Matrices,
            Colors = frame.Colors,
            Count = frame.Count,
            OwnerId = _ownerId
        }.Schedule();
        JobHandle disposeTransforms = transforms.Dispose(prepareHandle);
        JobHandle disposeBlocks = blocks.Dispose(prepareHandle);
        frame.Handle = JobHandle.CombineDependencies(disposeTransforms, disposeBlocks);
        frame.Pending = true;
    }
    private void CachePreparedRenderFrame(RenderFrameData frame)
    {
        int count = frame.Count.Value;
        int sourceIndex = 0;
        int batchIndex = 0;
        while (sourceIndex < count)
        {
            Matrix4x4[] matrices = _renderBatchMatrices[batchIndex];
            Vector4[] colors = _renderBatchColors[batchIndex];
            int batchCount = Mathf.Min(matrices.Length, count - sourceIndex);
            for (int i = 0; i < batchCount; i++)
            {
                float4x4 matrix = frame.Matrices[sourceIndex + i];
                matrices[i] = new Matrix4x4(matrix.c0, matrix.c1, matrix.c2, matrix.c3);
                colors[i] = frame.Colors[sourceIndex + i];
            }
            _renderBatchCounts[batchIndex] = batchCount;
            sourceIndex += batchCount;
            batchIndex++;
        }
        _renderBatchCount = batchIndex;
    }
    private void DrawCachedRenderFrame()
    {
        for (int i = 0; i < _renderBatchCount; i++)
        {
            Vector4[] colors = _renderBatchColors[i];
            _releasedBlockPropertyBlock.Clear();
            _releasedBlockPropertyBlock.SetVectorArray(ColorId, colors);
            Graphics.DrawMeshInstanced(_releasedBlockMesh, 0, _releasedBlockMaterial, _renderBatchMatrices[i],
                _renderBatchCounts[i], _releasedBlockPropertyBlock, ShadowCastingMode.Off, false, gameObject.layer);
        }
    }
    private void TrimReleasedBlockEntityList()
    {
        if (!EnsureEcsReady())
            return;
        for (int i = _releasedBlockEntities.Count - 1; i >= 0; i--)
            if (!_entityManager.Exists(_releasedBlockEntities[i]))
                _releasedBlockEntities.RemoveAt(i);
    }
    private void ClearReleasedBlockEntities()
    {
        if (!EnsureEcsReady())
        {
            _releasedBlockEntities.Clear();
            return;
        }
        for (int i = _releasedBlockEntities.Count - 1; i >= 0; i--)
            DestroyReleasedBlockEntityAt(i);
        using NativeArray<Entity> entities = _releasedBlockQuery.ToEntityArray(Allocator.Temp);
        using NativeArray<ReleasedBlockComponent> blocks =
            _releasedBlockQuery.ToComponentDataArray<ReleasedBlockComponent>(Allocator.Temp);
        for (int i = 0; i < entities.Length; i++)
            if (blocks[i].OwnerId == _ownerId && _entityManager.Exists(entities[i]))
                _entityManager.DestroyEntity(entities[i]);
    }
    private void DespawnInBounds(Bounds bounds)
    {
        if (!EnsureEcsReady())
            return;
        using NativeArray<Entity> entities = _releasedBlockQuery.ToEntityArray(Allocator.Temp);
        using NativeArray<LocalTransform> transforms =
            _releasedBlockQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        using NativeArray<ReleasedBlockComponent> blocks =
            _releasedBlockQuery.ToComponentDataArray<ReleasedBlockComponent>(Allocator.Temp);
        for (int i = 0; i < entities.Length; i++)
        {
            if (blocks[i].OwnerId != _ownerId)
                continue;
            float3 position = transforms[i].Position;
            if (bounds.Contains(new Vector3(position.x, position.y, position.z)) && _entityManager.Exists(entities[i]))
                _entityManager.DestroyEntity(entities[i]);
        }
    }
    private void ApplyConveyor(Bounds bounds, Vector3 direction, float speed, float acceleration, float deltaTime)
    {
        if (!EnsureEcsReady())
            return;
        using NativeArray<Entity> entities = _releasedBlockQuery.ToEntityArray(Allocator.Temp);
        using NativeArray<LocalTransform> transforms =
            _releasedBlockQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        using NativeArray<ReleasedBlockComponent> blocks =
            _releasedBlockQuery.ToComponentDataArray<ReleasedBlockComponent>(Allocator.Temp);
        float3 conveyorDirection = new float3(direction.x, direction.y, direction.z);
        for (int i = 0; i < entities.Length; i++)
        {
            if (blocks[i].OwnerId != _ownerId)
                continue;
            float3 position = transforms[i].Position;
            if (!bounds.Contains(new Vector3(position.x, position.y, position.z)))
                continue;
            PhysicsVelocity velocity = _entityManager.GetComponentData<PhysicsVelocity>(entities[i]);
            float currentSpeed = math.dot(velocity.Linear, conveyorDirection);
            velocity.Linear += conveyorDirection *
                               (Mathf.MoveTowards(currentSpeed, speed, acceleration * deltaTime) - currentSpeed);
            _entityManager.SetComponentData(entities[i], velocity);
        }
    }
    private void ApplySuction(Vector3 origin, Quaternion rotation, Vector3 boxSize, float force, float acceleration,
        float maxVelocity, float arrivalDamping, float destroyRadius, float deltaTime)
    {
        if (!EnsureEcsReady())
            return;
        Quaternion inverseRotation = Quaternion.Inverse(rotation);
        Vector3 halfSize = boxSize * 0.5f;
        Vector3 boxOffset = rotation * new Vector3(boxSize.x * 0.5f, 0f, 0f);
        using NativeArray<Entity> entities = _releasedBlockQuery.ToEntityArray(Allocator.Temp);
        using NativeArray<LocalTransform> transforms =
            _releasedBlockQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        using NativeArray<ReleasedBlockComponent> blocks =
            _releasedBlockQuery.ToComponentDataArray<ReleasedBlockComponent>(Allocator.Temp);
        int suckedBlockCount = 0;
        for (int i = 0; i < entities.Length; i++)
        {
            if (blocks[i].OwnerId != _ownerId)
                continue;
            float3 transformPosition = transforms[i].Position;
            Vector3 position = new Vector3(transformPosition.x, transformPosition.y, transformPosition.z);
            Vector3 local = inverseRotation * (position - origin - boxOffset);
            if (Mathf.Abs(local.x) > halfSize.x || Mathf.Abs(local.y) > halfSize.y || Mathf.Abs(local.z) > halfSize.z)
                continue;
            Vector3 direction = origin - position;
            direction.z = 0f;
            float distance = direction.magnitude;
            if (distance < destroyRadius)
            {
                if (_entityManager.Exists(entities[i]))
                {
                    _entityManager.DestroyEntity(entities[i]);
                    suckedBlockCount++;
                    SuckedBlockCount++;
                }
                continue;
            }
            direction /= distance;
            ReleasedBlockComponent block = blocks[i];
            block.StableFrames = 0;
            _entityManager.SetComponentData(entities[i], block);
            _entityManager.SetComponentEnabled<Simulate>(entities[i], true);
            PhysicsGravityFactor gravity = _entityManager.GetComponentData<PhysicsGravityFactor>(entities[i]);
            gravity.Value = 1f;
            _entityManager.SetComponentData(entities[i], gravity);
            PhysicsVelocity velocity = _entityManager.GetComponentData<PhysicsVelocity>(entities[i]);
            Vector3 currentVelocity = new Vector3(velocity.Linear.x, velocity.Linear.y, velocity.Linear.z);
            Vector3 targetVelocity = direction * Mathf.Min(force * distance, maxVelocity);
            Vector3 movedVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, acceleration * deltaTime);
            if (distance <= destroyRadius * 2.5f)
                movedVelocity = Vector3.Lerp(movedVelocity, targetVelocity, arrivalDamping * deltaTime);
            movedVelocity = Vector3.ClampMagnitude(movedVelocity, maxVelocity);
            velocity.Linear = new float3(movedVelocity.x, movedVelocity.y, movedVelocity.z);
            _entityManager.SetComponentData(entities[i], velocity);
        }

        if (suckedBlockCount > 0)
            BlocksSucked?.Invoke(suckedBlockCount);
    }
    private void DestroyReleasedBlockEntityAt(int index)
    {
        if (!EnsureEcsReady())
            return;
        Entity entity = _releasedBlockEntities[index];
        _releasedBlockEntities.RemoveAt(index);
        if (_entityManager.Exists(entity))
            _entityManager.DestroyEntity(entity);
    }
    private void DisposeReleasedBlocks()
    {
        ClearReleasedBlockEntities();
        _releasedBlockEntities.Clear();
    }
    private void DisposeReleasedBlockResources()
    {
        DisposeRenderFrameResources();
        if (_releasedBlockCollider.IsCreated)
        {
            _releasedBlockCollider.Dispose();
            _releasedBlockCollider = default;
        }
        if (_releasedBlockMaterial != null)
            DestroyUnityObject(_releasedBlockMaterial);
        _releasedBlockMaterial = null;
        _releasedBlockMesh = null;
    }
    private void EnsureRenderFrameResources()
    {
        if (_renderBatchMatrices == null)
        {
            int batchCapacity = Mathf.CeilToInt(_maxReleasedPhysicsBlocks / 1023f);
            _renderBatchMatrices = new Matrix4x4[batchCapacity][];
            _renderBatchColors = new Vector4[batchCapacity][];
            _renderBatchCounts = new int[batchCapacity];
            for (int i = 0; i < batchCapacity; i++)
            {
                _renderBatchMatrices[i] = new Matrix4x4[1023];
                _renderBatchColors[i] = new Vector4[1023];
            }
        }
        for (int i = 0; i < _renderFrames.Length; i++)
        {
            if (_renderFrames[i] != null)
                continue;
            _renderFrames[i] = new RenderFrameData(_maxReleasedPhysicsBlocks);
        }
    }
    private void DisposeRenderFrameResources()
    {
        for (int i = 0; i < _renderFrames.Length; i++)
        {
            RenderFrameData frame = _renderFrames[i];
            if (frame == null)
                continue;
            if (frame.Pending)
                frame.Handle.Complete();
            frame.Dispose();
            _renderFrames[i] = null;
        }
        _displayRenderFrame = -1;
        _renderBatchCount = 0;
        _renderBatchMatrices = null;
        _renderBatchColors = null;
        _renderBatchCounts = null;
    }
    private void DisposeReleasedBlockWalls()
    {
        if (_ecsWorld != null && _ecsWorld.IsCreated)
        {
            for (int i = 0; i < _releasedBlockWallEntities.Count; i++)
            {
                Entity entity = _releasedBlockWallEntities[i];
                if (_entityManager.Exists(entity))
                    _entityManager.DestroyEntity(entity);
            }
        }
        _releasedBlockWallEntities.Clear();
        for (int i = 0; i < _releasedBlockWallColliders.Count; i++)
        {
            if (_releasedBlockWallColliders[i].IsCreated)
                _releasedBlockWallColliders[i].Dispose();
        }
        _releasedBlockWallColliders.Clear();
    }
    private void DisposeEcsQuery()
    {
        if (_hasReleasedBlockQuery && _ecsWorld != null && _ecsWorld.IsCreated)
        {
            _releasedBlockQuery.Dispose();
        }
        _releasedBlockQuery = default;
        _hasReleasedBlockQuery = false;
    }
    private PhysicsMaterial CreatePhysicsMaterial()
    {
        PhysicsMaterial material = PhysicsMaterial.Default;
        material.Friction = _physicsFriction;
        material.Restitution = _physicsRestitution;
        return material;
    }
    private static Vector4 ToFloat4(Color32 color)
    {
        return new Vector4(color.r / 255f, color.g / 255f, color.b / 255f, color.a / 255f);
    }
    private static void DestroyUnityObject(Object target)
    {
        if (Application.isPlaying) Destroy(target);
        else DestroyImmediate(target);
    }
    private static float GetVoxelRotationY(int x, int y, float maxAngle)
    {
        if (maxAngle <= 0f)
            return 0f;
        uint hash = (uint)(x * 73856093) ^ (uint)(y * 19349663);
        float normalized = (hash & 1023u) * (1f / 1023f);
        return (normalized * 2f - 1f) * maxAngle;
    }
    private void DisposeCells()
    {
        if (_cellColors.IsCreated)
            _cellColors.Dispose();
        if (_cellSolid.IsCreated)
            _cellSolid.Dispose();
    }
    internal struct TextureBlockMeshBuilder
    {
        [ReadOnly] public NativeArray<Color32> CellColors;
        [ReadOnly] public NativeArray<byte> CellSolid;
        public NativeArray<byte> Visited;
        public NativeList<Vector3> Vertices;
        public NativeList<Color32> Colors;
        public NativeList<Vector2> Uvs;
        public NativeList<int> Indices;
        public int GridWidth;
        public int StartX;
        public int StartY;
        public int ChunkWidth;
        public int ChunkHeight;
        public float CellSize;
        public Vector3 Offset;
        public byte UseVoxelDetail;
        public byte ExtrudeMergedQuads;
        public byte MergeAnySolid;
        public float DetailVoxelScale;
        public float RandomYRotation;
        public float DetailDepth;
        public void Execute()
        {
            for (int i = 0; i < Visited.Length; i++)
                Visited[i] = 0;
            for (int y = 0; y < ChunkHeight; y++)
            {
                for (int x = 0; x < ChunkWidth; x++)
                {
                    int localIndex = y * ChunkWidth + x;
                    if (Visited[localIndex] != 0)
                        continue;
                    int cellIndex = (StartY + y) * GridWidth + StartX + x;
                    if (CellSolid[cellIndex] == 0)
                        continue;
                    Color32 color = CellColors[cellIndex];
                    if (UseVoxelDetail != 0)
                    {
                        Visited[localIndex] = 1;
                        AddVoxelBox(x, y, color);
                        continue;
                    }
                    int rectWidth = 1;
                    while (x + rectWidth < ChunkWidth && CanMerge(x + rectWidth, y, color))
                        rectWidth++;
                    int rectHeight = 1;
                    bool canGrow = true;
                    while (y + rectHeight < ChunkHeight && canGrow)
                    {
                        for (int scanX = 0; scanX < rectWidth; scanX++)
                        {
                            if (!CanMerge(x + scanX, y + rectHeight, color))
                            {
                                canGrow = false;
                                break;
                            }
                        }
                        if (canGrow)
                            rectHeight++;
                    }
                    for (int fillY = 0; fillY < rectHeight; fillY++)
                    {
                        for (int fillX = 0; fillX < rectWidth; fillX++)
                            Visited[(y + fillY) * ChunkWidth + x + fillX] = 1;
                    }
                    if (ExtrudeMergedQuads != 0)
                        AddMergedBox(x, y, rectWidth, rectHeight, color);
                    else
                        AddQuad(x, y, rectWidth, rectHeight, color);
                }
            }
        }
        private bool CanMerge(int x, int y, Color32 color)
        {
            int localIndex = y * ChunkWidth + x;
            int cellIndex = (StartY + y) * GridWidth + StartX + x;
            if (Visited[localIndex] != 0 || CellSolid[cellIndex] == 0)
                return false;
            Color32 other = CellColors[cellIndex];
            if (MergeAnySolid != 0)
                return true;
            return other.r == color.r && other.g == color.g && other.b == color.b && other.a == color.a;
        }
        private void AddQuad(int x, int y, int width, int height, Color32 color)
        {
            float minX = Offset.x + (StartX + x) * CellSize - CellSize * 0.5f;
            float minY = Offset.y + (StartY + y) * CellSize - CellSize * 0.5f;
            float maxX = minX + width * CellSize;
            float maxY = minY + height * CellSize;
            int vertexIndex = Vertices.Length;
            Vertices.Add(new Vector3(minX, minY, 0f));
            Vertices.Add(new Vector3(minX, maxY, 0f));
            Vertices.Add(new Vector3(maxX, maxY, 0f));
            Vertices.Add(new Vector3(maxX, minY, 0f));
            AddVertexData(color);
            AddQuadIndices(vertexIndex);
        }
        private void AddVoxelBox(int x, int y, Color32 color)
        {
            float half = CellSize * DetailVoxelScale * 0.5f;
            float halfDepth = DetailDepth * 0.5f;
            float centerX = Offset.x + (StartX + x) * CellSize;
            float centerY = Offset.y + (StartY + y) * CellSize;
            float rotationY = GetRotationY(StartX + x, StartY + y);
            Vector3 frontMin = new Vector3(centerX - half, centerY - half, -halfDepth);
            Vector3 frontMax = new Vector3(centerX + half, centerY + half, -halfDepth);
            Vector3 backMin = new Vector3(centerX - half, centerY - half, halfDepth);
            Vector3 backMax = new Vector3(centerX + half, centerY + half, halfDepth);
            AddFace(
                RotateAroundCenter(new Vector3(frontMin.x, frontMin.y, frontMin.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(frontMin.x, frontMax.y, frontMin.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(frontMax.x, frontMax.y, frontMin.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(frontMax.x, frontMin.y, frontMin.z), centerX, rotationY),
                color);
            AddFace(
                RotateAroundCenter(new Vector3(backMax.x, backMin.y, backMax.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(backMax.x, backMax.y, backMax.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(backMin.x, backMax.y, backMax.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(backMin.x, backMin.y, backMax.z), centerX, rotationY),
                color);
            AddFace(
                RotateAroundCenter(new Vector3(frontMin.x, frontMin.y, frontMin.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(backMin.x, backMin.y, backMin.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(backMin.x, backMax.y, backMax.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(frontMin.x, frontMax.y, frontMin.z), centerX, rotationY),
                color);
            AddFace(
                RotateAroundCenter(new Vector3(frontMax.x, frontMin.y, frontMin.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(frontMax.x, frontMax.y, frontMin.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(backMax.x, backMax.y, backMax.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(backMax.x, backMin.y, backMax.z), centerX, rotationY),
                color);
            AddFace(
                RotateAroundCenter(new Vector3(frontMin.x, frontMax.y, frontMin.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(backMin.x, backMax.y, backMax.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(backMax.x, backMax.y, backMax.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(frontMax.x, frontMax.y, frontMin.z), centerX, rotationY),
                color);
            AddFace(
                RotateAroundCenter(new Vector3(frontMin.x, frontMin.y, frontMin.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(frontMax.x, frontMin.y, frontMin.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(backMax.x, backMin.y, backMax.z), centerX, rotationY),
                RotateAroundCenter(new Vector3(backMin.x, backMin.y, backMin.z), centerX, rotationY),
                color);
        }
        private void AddMergedBox(int x, int y, int width, int height, Color32 color)
        {
            float minX = Offset.x + (StartX + x) * CellSize - CellSize * 0.5f;
            float minY = Offset.y + (StartY + y) * CellSize - CellSize * 0.5f;
            float maxX = minX + width * CellSize;
            float maxY = minY + height * CellSize;
            float halfDepth = DetailDepth * 0.5f;
            Vector3 frontMin = new Vector3(minX, minY, -halfDepth);
            Vector3 frontMax = new Vector3(maxX, maxY, -halfDepth);
            Vector3 backMin = new Vector3(minX, minY, halfDepth);
            Vector3 backMax = new Vector3(maxX, maxY, halfDepth);
            AddFace(new Vector3(frontMin.x, frontMin.y, frontMin.z), new Vector3(frontMin.x, frontMax.y, frontMin.z),
                new Vector3(frontMax.x, frontMax.y, frontMin.z), new Vector3(frontMax.x, frontMin.y, frontMin.z),
                color);
            AddFace(new Vector3(backMax.x, backMin.y, backMax.z), new Vector3(backMax.x, backMax.y, backMax.z),
                new Vector3(backMin.x, backMax.y, backMax.z), new Vector3(backMin.x, backMin.y, backMax.z), color);
            AddFace(new Vector3(frontMin.x, frontMin.y, frontMin.z), new Vector3(backMin.x, backMin.y, backMin.z),
                new Vector3(backMin.x, backMax.y, backMax.z), new Vector3(frontMin.x, frontMax.y, frontMin.z),
                color);
            AddFace(new Vector3(frontMax.x, frontMin.y, frontMin.z), new Vector3(frontMax.x, frontMax.y, frontMin.z),
                new Vector3(backMax.x, backMax.y, backMax.z), new Vector3(backMax.x, backMin.y, backMax.z), color);
            AddFace(new Vector3(frontMin.x, frontMax.y, frontMin.z), new Vector3(backMin.x, backMax.y, backMax.z),
                new Vector3(backMax.x, backMax.y, backMax.z), new Vector3(frontMax.x, frontMax.y, frontMin.z),
                color);
            AddFace(new Vector3(frontMin.x, frontMin.y, frontMin.z), new Vector3(frontMax.x, frontMin.y, frontMin.z),
                new Vector3(backMax.x, backMin.y, backMax.z), new Vector3(backMin.x, backMin.y, backMin.z), color);
        }
        private float GetRotationY(int x, int y)
        {
            if (RandomYRotation <= 0f)
                return 0f;
            uint hash = (uint)(x * 73856093) ^ (uint)(y * 19349663);
            float normalized = (hash & 1023u) * (1f / 1023f);
            return (normalized * 2f - 1f) * RandomYRotation;
        }
        private Vector3 RotateAroundCenter(Vector3 point, float centerX, float degrees)
        {
            if (degrees == 0f)
                return point;
            float radians = degrees * 0.0174532924f;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            float localX = point.x - centerX;
            float rotatedX = localX * cos + point.z * sin;
            float rotatedZ = -localX * sin + point.z * cos;
            return new Vector3(centerX + rotatedX, point.y, rotatedZ);
        }
        private void AddFace(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 color)
        {
            int vertexIndex = Vertices.Length;
            Vertices.Add(a);
            Vertices.Add(b);
            Vertices.Add(c);
            Vertices.Add(d);
            AddVertexData(color);
            AddQuadIndices(vertexIndex);
        }
        private void AddVertexData(Color32 color)
        {
            Colors.Add(color);
            Colors.Add(color);
            Colors.Add(color);
            Colors.Add(color);
            Uvs.Add(new Vector2(0f, 0f));
            Uvs.Add(new Vector2(0f, 1f));
            Uvs.Add(new Vector2(1f, 1f));
            Uvs.Add(new Vector2(1f, 0f));
        }
        private void AddQuadIndices(int vertexIndex)
        {
            Indices.Add(vertexIndex);
            Indices.Add(vertexIndex + 1);
            Indices.Add(vertexIndex + 2);
            Indices.Add(vertexIndex);
            Indices.Add(vertexIndex + 2);
            Indices.Add(vertexIndex + 3);
        }
    }
}
