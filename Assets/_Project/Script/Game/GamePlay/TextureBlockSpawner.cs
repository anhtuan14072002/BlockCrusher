using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class TextureBlockSpawner : MonoBehaviour
{
    private static readonly List<TextureBlockSpawner> ActiveSpawners = new();
    private static readonly Vector2[] ReleasedBlockUvs =
    {
        new Vector2(0f, 0f),
        new Vector2(0f, 1f),
        new Vector2(1f, 1f),
        new Vector2(1f, 0f),
        new Vector2(0f, 0f),
        new Vector2(0f, 1f),
        new Vector2(1f, 1f),
        new Vector2(1f, 0f),
        new Vector2(0f, 0f),
        new Vector2(0f, 1f),
        new Vector2(1f, 1f),
        new Vector2(1f, 0f),
        new Vector2(0f, 0f),
        new Vector2(0f, 1f),
        new Vector2(1f, 1f),
        new Vector2(1f, 0f),
        new Vector2(0f, 0f),
        new Vector2(0f, 1f),
        new Vector2(1f, 1f),
        new Vector2(1f, 0f),
        new Vector2(0f, 0f),
        new Vector2(0f, 1f),
        new Vector2(1f, 1f),
        new Vector2(1f, 0f)
    };

    private static readonly int[] ReleasedBlockIndices =
    {
        0, 1, 2, 0, 2, 3,
        4, 5, 6, 4, 6, 7,
        8, 9, 10, 8, 10, 11,
        12, 13, 14, 12, 14, 15,
        16, 17, 18, 16, 18, 19,
        20, 21, 22, 20, 22, 23
    };

    [SerializeField] private Texture2D _texture;
    [SerializeField] private Transform _container;
    [SerializeField] private Material _chunkMaterial;
    [SerializeField] private float _pixelSize = 0.12f;
    [SerializeField, Range(1, 16)] private int _sampleStep = 1;
    [SerializeField, Range(0f, 1f)] private float _alphaThreshold = 0.1f;
    [SerializeField] private float _chunkColliderDepth = 0.25f;
    [SerializeField] private float _sawReleaseRadius = 0.18f;
    [SerializeField] private bool _renderVoxelDetailFromStart = true;
    [SerializeField, Range(0.75f, 1f)] private float _detailVoxelScale = 0.94f;
    [SerializeField, Range(0f, 30f)] private float _voxelRandomYRotation = 20f;
    [SerializeField, Range(0, 32)] private int _maxPhysicsDebrisPerFrame = 8;
    [SerializeField, Range(8, 64)] private int _chunkSize = 24;
    [SerializeField, Range(1, 8)] private int _maxChunkRebuildsPerFrame = 2;
    [SerializeField] private float _releasedBlockGravity = 3f;
    [SerializeField, Range(0f, 20f)] private float _releasedBlockDamping = 2.5f;
    [SerializeField, Range(0f, 1f)] private float _releasedBlockRestitution = 0.5f;
    [SerializeField, Range(0f, 1f)] private float _releasedBlockFriction = 0.2f;
    [SerializeField] private bool _centerTexture = true;
    [SerializeField] private bool _spawnOnAwake = true;

    private readonly List<ChunkRuntime> _chunks = new();
    private readonly List<int> _dirtyChunks = new(16);
    private readonly List<int> _scheduledChunkRebuilds = new(8);
    private readonly List<JobHandle> _scheduledChunkHandles = new(8);
    private NativeArray<Color32> _cellColors;
    private NativeArray<byte> _cellSolid;
    private Mesh _releasedBlockMesh;
    private Material _releasedBlockMaterial;
    private Matrix4x4[] _instanceMatrices;
    private Vector4[] _instanceColors;
    private MaterialPropertyBlock _instancePropertyBlock;
    private World _ecsWorld;
    private EntityManager _entityManager;
    private Entity _ecsOwner;
    private EntityQuery _releasedBlockQuery;
    private bool _hasReleasedBlockQuery;
    private byte[] _chunkDirty;
    private Transform _runtimeParent;
    private Material _runtimeChunkMaterial;
    private Vector3 _offset;
    private int _gridWidth;
    private int _gridHeight;
    private int _chunkColumns;
    private float _cellSize;
    private int _physicsDebrisFrame = -1;
    private int _physicsDebrisSpawnedThisFrame;
    private Vector3 _sawForcePosition;
    private float _sawForceRadius;
    private float _sawForceStrength;

    private static readonly int ColorId = Shader.PropertyToID("_Color");

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

    public static void ApplySawForceForActiveSpawners(Vector3 sawPosition, float radius, float strength)
    {
        for (int i = 0; i < ActiveSpawners.Count; i++)
        {
            TextureBlockSpawner spawner = ActiveSpawners[i];
            spawner._sawForcePosition = sawPosition;
            spawner._sawForceRadius = radius;
            spawner._sawForceStrength = strength;
            spawner.UpdateEcsSpawnerData();
        }
    }

    private void OnEnable()
    {
        if (!ActiveSpawners.Contains(this))
            ActiveSpawners.Add(this);
    }

    private void OnDisable()
    {
        ActiveSpawners.Remove(this);
    }

    private void Awake()
    {
        if (_spawnOnAwake)
            Spawn();
    }

    private void OnDestroy()
    {
        DisposeChunks();
        DisposeReleasedBlocks();
        DisposeCells();
        DestroyEcsOwner();
    }

    private void FixedUpdate()
    {
        UpdateEcsSpawnerData();
    }

    private void LateUpdate()
    {
        RenderReleasedBlocks();

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
        CreateEcsOwner();
        SyncAllCellsToEcs();
        CreateChunks();
        CreateReleasedBlockRendering();
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
                SetEcsCellSolid(cellIndex, 0);
                if (TryConsumePhysicsDebrisBudget())
                {
                    SpawnReleasedBlock(cellLocal, color, worldPoint, pressDirection, pressSpeed, outwardForce,
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
        if (_maxPhysicsDebrisPerFrame <= 0)
            return false;

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
        Material material = _runtimeChunkMaterial;
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
            Indices = new NativeList<int>(indexCapacity, Allocator.Persistent)
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
                DetailVoxelScale = _detailVoxelScale,
                RandomYRotation = _voxelRandomYRotation,
                DetailDepth = _chunkColliderDepth
            };

            _scheduledChunkHandles.Add(meshJob.Schedule());
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

            ApplyChunkMesh(_chunks[chunkIndex]);
        }
    }

    private void ApplyChunkMesh(ChunkRuntime chunk)
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
        }
        else
        {
            chunk.Renderer.enabled = false;
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
            if (chunk.Mesh != null)
                DestroyUnityObject(chunk.Mesh);
        }

        _chunks.Clear();
    }

    private Material ResolveChunkMaterial()
    {
        if (_chunkMaterial != null)
            return _chunkMaterial;

        Shader voxelShader = Shader.Find("BlockCrusher/VoxelExactColor");
        if (voxelShader != null)
            return new Material(voxelShader);

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
            return new Material(shader);

        return null;
    }

    private Vector3 GetCellLocalPosition(int x, int y)
    {
        return _offset + new Vector3(x * _cellSize, y * _cellSize, 0f);
    }

    private void SpawnReleasedBlock(Vector3 localPosition, Color32 color, Vector3 sawCenter, Vector3 pressDirection,
        float pressSpeed, float outwardForce, float tangentialForce, float spinDirection, float bladeRadius,
        float sideDamping, float maxVelocity)
    {
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
                           (tangentialForce * 0.42f * pushScale * speedScale * radiusPush);
        velocity -= outward * Vector3.Dot(velocity, outward) * sideDamping * radiusPush * 0.15f;

        if (!HasValidEcsOwner())
            return;

        Entity block = _entityManager.CreateEntity(ComponentType.ReadWrite<ReleasedBlockData>());
        Vector3 clampedVelocity = Vector3.ClampMagnitude(velocity, maxVelocity);
        _entityManager.SetComponentData(block, new ReleasedBlockData
        {
            Owner = _ecsOwner,
            Position = new float3(position.x, position.y, position.z),
            Velocity = new float3(clampedVelocity.x, clampedVelocity.y, clampedVelocity.z),
            Color = new float4(color.r / 255f, color.g / 255f, color.b / 255f, color.a / 255f)
        });
    }

    private void CreateReleasedBlockRendering()
    {
        DisposeReleasedBlocks();
        _releasedBlockMaterial = _runtimeChunkMaterial != null ? _runtimeChunkMaterial : ResolveChunkMaterial();
        if (_releasedBlockMaterial != null)
            _releasedBlockMaterial.enableInstancing = true;
        _releasedBlockMesh = CreateReleasedBlockMesh();
        _instancePropertyBlock = new MaterialPropertyBlock();
    }

    private Mesh CreateReleasedBlockMesh()
    {
        float half = _detailVoxelScale * 0.5f;
        float halfDepth = _chunkColliderDepth / Mathf.Max(_cellSize, 0.0001f) * 0.5f;
        Vector3[] vertices = new Vector3[24];
        FillFace(vertices, 0, 0f, new Vector3(-half, -half, -halfDepth), new Vector3(-half, half, -halfDepth),
            new Vector3(half, half, -halfDepth), new Vector3(half, -half, -halfDepth));
        FillFace(vertices, 4, 0f, new Vector3(half, -half, halfDepth), new Vector3(half, half, halfDepth),
            new Vector3(-half, half, halfDepth), new Vector3(-half, -half, halfDepth));
        FillFace(vertices, 8, 0f, new Vector3(-half, -half, -halfDepth), new Vector3(-half, -half, halfDepth),
            new Vector3(-half, half, halfDepth), new Vector3(-half, half, -halfDepth));
        FillFace(vertices, 12, 0f, new Vector3(half, -half, -halfDepth), new Vector3(half, half, -halfDepth),
            new Vector3(half, half, halfDepth), new Vector3(half, -half, halfDepth));
        FillFace(vertices, 16, 0f, new Vector3(-half, half, -halfDepth), new Vector3(-half, half, halfDepth),
            new Vector3(half, half, halfDepth), new Vector3(half, half, -halfDepth));
        FillFace(vertices, 20, 0f, new Vector3(-half, -half, -halfDepth), new Vector3(half, -half, -halfDepth),
            new Vector3(half, -half, halfDepth), new Vector3(-half, -half, halfDepth));
        Mesh mesh = new Mesh { name = "ReleasedBlock_InstancedMesh" };
        mesh.vertices = vertices;
        mesh.uv = ReleasedBlockUvs;
        mesh.triangles = ReleasedBlockIndices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void RenderReleasedBlocks()
    {
        if (!HasValidEcsOwner() || _releasedBlockMesh == null || _releasedBlockMaterial == null)
            return;

        using NativeArray<ReleasedBlockData> blocks =
            _releasedBlockQuery.ToComponentDataArray<ReleasedBlockData>(Allocator.Temp);
        int count = CountOwnedBlocks(blocks);
        if (count == 0)
            return;

        EnsureInstanceCapacity(Mathf.Min(count, 1023));
        int batchCount = 0;
        for (int i = 0; i < blocks.Length; i++)
        {
            ReleasedBlockData block = blocks[i];
            if (block.Owner != _ecsOwner)
                continue;

            _instanceMatrices[batchCount] = Matrix4x4.TRS(ToVector3(block.Position), Quaternion.identity,
                Vector3.one * _cellSize);
            _instanceColors[batchCount] = ToVector4(block.Color);
            batchCount++;

            if (batchCount < 1023)
                continue;

            DrawReleasedBlockBatch(batchCount);
            batchCount = 0;
        }

        if (batchCount > 0)
            DrawReleasedBlockBatch(batchCount);
    }

    private void DrawReleasedBlockBatch(int batchCount)
    {
        _instancePropertyBlock.Clear();
        _instancePropertyBlock.SetVectorArray(ColorId, _instanceColors);
        Graphics.DrawMeshInstanced(_releasedBlockMesh, 0, _releasedBlockMaterial, _instanceMatrices, batchCount,
            _instancePropertyBlock, ShadowCastingMode.Off, false, gameObject.layer);
    }

    private int CountOwnedBlocks(NativeArray<ReleasedBlockData> blocks)
    {
        int count = 0;
        for (int i = 0; i < blocks.Length; i++)
            if (blocks[i].Owner == _ecsOwner)
                count++;

        return count;
    }

    private void EnsureInstanceCapacity(int capacity)
    {
        if (_instanceMatrices != null && _instanceMatrices.Length >= capacity)
            return;
        _instanceMatrices = new Matrix4x4[Mathf.NextPowerOfTwo(capacity)];
        _instanceColors = new Vector4[_instanceMatrices.Length];
    }

    private void CreateEcsOwner()
    {
        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated)
            return;

        _ecsWorld = world;
        _entityManager = world.EntityManager;
        ClearReleasedBlockEntities();
        if (_entityManager.Exists(_ecsOwner))
            _entityManager.DestroyEntity(_ecsOwner);
        if (_hasReleasedBlockQuery)
        {
            _releasedBlockQuery.Dispose();
            _hasReleasedBlockQuery = false;
        }

        _ecsOwner = _entityManager.CreateEntity(ComponentType.ReadWrite<TextureBlockSpawnerData>());
        _entityManager.AddBuffer<TextureBlockCell>(_ecsOwner);
        _releasedBlockQuery = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<ReleasedBlockData>());
        _hasReleasedBlockQuery = true;
        UpdateEcsSpawnerData();
    }

    private void UpdateEcsSpawnerData()
    {
        if (!HasValidEcsOwner() || _runtimeParent == null)
            return;

        _entityManager.SetComponentData(_ecsOwner, new TextureBlockSpawnerData
        {
            GridWidth = _gridWidth,
            GridHeight = _gridHeight,
            CellSize = _cellSize,
            Offset = new float3(_offset.x, _offset.y, _offset.z),
            WorldToLocal = ToFloat4x4(_runtimeParent.worldToLocalMatrix),
            LocalToWorld = ToFloat4x4(_runtimeParent.localToWorldMatrix),
            Gravity = _releasedBlockGravity,
            Damping = _releasedBlockDamping,
            Restitution = _releasedBlockRestitution,
            Friction = _releasedBlockFriction,
            SawPosition = new float3(_sawForcePosition.x, _sawForcePosition.y, _sawForcePosition.z),
            SawRadius = _sawForceRadius,
            SawForce = _sawForceStrength
        });
    }

    private void SyncAllCellsToEcs()
    {
        if (!HasValidEcsOwner() || !_cellSolid.IsCreated)
            return;

        DynamicBuffer<TextureBlockCell> cells = _entityManager.GetBuffer<TextureBlockCell>(_ecsOwner);
        cells.ResizeUninitialized(_cellSolid.Length);
        for (int i = 0; i < _cellSolid.Length; i++)
            cells[i] = new TextureBlockCell { Solid = _cellSolid[i] };
    }

    private void SetEcsCellSolid(int cellIndex, byte solid)
    {
        if (!HasValidEcsOwner())
            return;

        DynamicBuffer<TextureBlockCell> cells = _entityManager.GetBuffer<TextureBlockCell>(_ecsOwner);
        if ((uint)cellIndex < (uint)cells.Length)
            cells[cellIndex] = new TextureBlockCell { Solid = solid };
    }

    private bool HasValidEcsOwner()
    {
        return _ecsWorld != null && _ecsWorld.IsCreated && _entityManager.Exists(_ecsOwner);
    }

    private void ClearReleasedBlockEntities()
    {
        if (_ecsWorld == null || !_ecsWorld.IsCreated || !_hasReleasedBlockQuery)
            return;

        using NativeArray<Entity> entities = _releasedBlockQuery.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < entities.Length; i++)
        {
            Entity entity = entities[i];
            ReleasedBlockData block = _entityManager.GetComponentData<ReleasedBlockData>(entity);
            if (block.Owner == _ecsOwner)
                _entityManager.DestroyEntity(entity);
        }
    }

    private static float4x4 ToFloat4x4(Matrix4x4 matrix)
    {
        return new float4x4(
            new float4(matrix.m00, matrix.m10, matrix.m20, matrix.m30),
            new float4(matrix.m01, matrix.m11, matrix.m21, matrix.m31),
            new float4(matrix.m02, matrix.m12, matrix.m22, matrix.m32),
            new float4(matrix.m03, matrix.m13, matrix.m23, matrix.m33));
    }

    private static Vector3 ToVector3(float3 value)
    {
        return new Vector3(value.x, value.y, value.z);
    }

    private static Vector4 ToVector4(float4 value)
    {
        return new Vector4(value.x, value.y, value.z, value.w);
    }

    private void DestroyEcsOwner()
    {
        if (_ecsWorld == null || !_ecsWorld.IsCreated)
            return;

        ClearReleasedBlockEntities();
        if (_entityManager.Exists(_ecsOwner))
            _entityManager.DestroyEntity(_ecsOwner);

        _ecsOwner = Entity.Null;
        if (_hasReleasedBlockQuery)
        {
            _releasedBlockQuery.Dispose();
            _hasReleasedBlockQuery = false;
        }
    }

    private void DespawnReleasedBlock(Entity entity)
    {
        if (HasValidEcsOwner() && _entityManager.Exists(entity))
            _entityManager.DestroyEntity(entity);
    }

    private void DespawnInBounds(Bounds bounds)
    {
        if (!HasValidEcsOwner())
            return;

        using NativeArray<Entity> entities = _releasedBlockQuery.ToEntityArray(Allocator.Temp);
        using NativeArray<ReleasedBlockData> blocks =
            _releasedBlockQuery.ToComponentDataArray<ReleasedBlockData>(Allocator.Temp);
        for (int i = 0; i < blocks.Length; i++)
            if (blocks[i].Owner == _ecsOwner && bounds.Contains(ToVector3(blocks[i].Position)))
                DespawnReleasedBlock(entities[i]);
    }

    private void ApplyConveyor(Bounds bounds, Vector3 direction, float speed, float acceleration, float deltaTime)
    {
        if (!HasValidEcsOwner())
            return;

        using NativeArray<Entity> entities = _releasedBlockQuery.ToEntityArray(Allocator.Temp);
        using NativeArray<ReleasedBlockData> blocks =
            _releasedBlockQuery.ToComponentDataArray<ReleasedBlockData>(Allocator.Temp);
        for (int i = 0; i < blocks.Length; i++)
        {
            ReleasedBlockData block = blocks[i];
            if (block.Owner != _ecsOwner || !bounds.Contains(ToVector3(block.Position)))
                continue;

            Vector3 velocity = ToVector3(block.Velocity);
            float currentSpeed = Vector3.Dot(velocity, direction);
            Vector3 newVelocity = velocity + direction *
                (Mathf.MoveTowards(currentSpeed, speed, acceleration * deltaTime) - currentSpeed);
            block.Velocity = new float3(newVelocity.x, newVelocity.y, newVelocity.z);
            _entityManager.SetComponentData(entities[i], block);
        }
    }

    private void ApplySuction(Vector3 origin, Quaternion rotation, Vector3 boxSize, float force, float acceleration,
        float maxVelocity, float arrivalDamping, float destroyRadius, float deltaTime)
    {
        if (!HasValidEcsOwner())
            return;

        Quaternion inverseRotation = Quaternion.Inverse(rotation);
        Vector3 halfSize = boxSize * 0.5f;
        using NativeArray<Entity> entities = _releasedBlockQuery.ToEntityArray(Allocator.Temp);
        using NativeArray<ReleasedBlockData> blocks =
            _releasedBlockQuery.ToComponentDataArray<ReleasedBlockData>(Allocator.Temp);
        for (int i = 0; i < blocks.Length; i++)
        {
            ReleasedBlockData block = blocks[i];
            if (block.Owner != _ecsOwner)
                continue;

            Vector3 position = ToVector3(block.Position);
            Vector3 local = inverseRotation * (position - origin - rotation * new Vector3(boxSize.x * 0.5f, 0f, 0f));
            if (Mathf.Abs(local.x) > halfSize.x || Mathf.Abs(local.y) > halfSize.y || Mathf.Abs(local.z) > halfSize.z)
                continue;

            Vector3 direction = origin - position;
            direction.z = 0f;
            float distance = direction.magnitude;
            if (distance < destroyRadius)
            {
                DespawnReleasedBlock(entities[i]);
                continue;
            }
            direction /= distance;
            Vector3 targetVelocity = direction * Mathf.Min(force * distance, maxVelocity);
            Vector3 velocity = Vector3.MoveTowards(ToVector3(block.Velocity), targetVelocity, acceleration * deltaTime);
            if (distance <= destroyRadius * 2.5f)
                velocity = Vector3.Lerp(velocity, targetVelocity, arrivalDamping * deltaTime);
            Vector3 clampedVelocity = Vector3.ClampMagnitude(velocity, maxVelocity);
            block.Velocity = new float3(clampedVelocity.x, clampedVelocity.y, clampedVelocity.z);
            _entityManager.SetComponentData(entities[i], block);
        }
    }

    private void DisposeReleasedBlocks()
    {
        ClearReleasedBlockEntities();
        if (_releasedBlockMesh != null) DestroyUnityObject(_releasedBlockMesh);
        _releasedBlockMesh = null;
    }

    private static void DestroyUnityObject(Object target)
    {
        if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
    }

    private static void FillFace(Vector3[] vertices, int startIndex, float rotationY, Vector3 a, Vector3 b, Vector3 c,
        Vector3 d)
    {
        vertices[startIndex] = RotateY(a, rotationY);
        vertices[startIndex + 1] = RotateY(b, rotationY);
        vertices[startIndex + 2] = RotateY(c, rotationY);
        vertices[startIndex + 3] = RotateY(d, rotationY);
    }

    private static Vector3 RotateY(Vector3 point, float degrees)
    {
        if (degrees == 0f)
            return point;

        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector3(point.x * cos + point.z * sin, point.y, -point.x * sin + point.z * cos);
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

    private struct ChunkRuntime
    {
        public Mesh Mesh;
        public MeshRenderer Renderer;
        public NativeArray<byte> Visited;
        public NativeList<Vector3> Vertices;
        public NativeList<Color32> Colors;
        public NativeList<Vector2> Uvs;
        public NativeList<int> Indices;
        public int StartX;
        public int StartY;
        public int Width;
        public int Height;
        public bool IsDetailed;
    }

    [BurstCompile]
    private struct TextureToCellsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Color32> TexturePixels;
        [WriteOnly] public NativeArray<Color32> CellColors;
        [WriteOnly] public NativeArray<byte> CellSolid;
        public int TextureWidth;
        public int TextureHeight;
        public int GridWidth;
        public int SampleStep;
        public byte AlphaLimit;

        public void Execute(int index)
        {
            int cellX = index % GridWidth;
            int cellY = index / GridWidth;
            int sourceX = cellX * SampleStep;
            int sourceY = cellY * SampleStep;
            if (sourceX >= TextureWidth)
                sourceX = TextureWidth - 1;
            if (sourceY >= TextureHeight)
                sourceY = TextureHeight - 1;

            Color32 color = TexturePixels[sourceY * TextureWidth + sourceX];
            bool solid = color.a > AlphaLimit;
            if (solid)
                color.a = 255;

            CellColors[index] = color;
            CellSolid[index] = solid ? (byte)1 : (byte)0;
        }
    }

    [BurstCompile]
    private struct BuildChunkMeshJob : IJob
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

            Colors.Add(color);
            Colors.Add(color);
            Colors.Add(color);
            Colors.Add(color);

            Uvs.Add(new Vector2(0f, 0f));
            Uvs.Add(new Vector2(0f, 1f));
            Uvs.Add(new Vector2(1f, 1f));
            Uvs.Add(new Vector2(1f, 0f));

            Indices.Add(vertexIndex);
            Indices.Add(vertexIndex + 1);
            Indices.Add(vertexIndex + 2);
            Indices.Add(vertexIndex);
            Indices.Add(vertexIndex + 2);
            Indices.Add(vertexIndex + 3);
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

            Colors.Add(color);
            Colors.Add(color);
            Colors.Add(color);
            Colors.Add(color);

            Uvs.Add(new Vector2(0f, 0f));
            Uvs.Add(new Vector2(0f, 1f));
            Uvs.Add(new Vector2(1f, 1f));
            Uvs.Add(new Vector2(1f, 0f));

            Indices.Add(vertexIndex);
            Indices.Add(vertexIndex + 1);
            Indices.Add(vertexIndex + 2);
            Indices.Add(vertexIndex);
            Indices.Add(vertexIndex + 2);
            Indices.Add(vertexIndex + 3);
        }
    }
}

public sealed class TextureBlockChunk : MonoBehaviour
{
    private TextureBlockSpawner _spawner;

    public void Initialize(TextureBlockSpawner spawner)
    {
        _spawner = spawner;
    }

    public bool ReleaseAtWorld(Vector3 worldPoint, Vector3 pressDirection, float pressSpeed, float outwardForce,
        float tangentialForce, float spinDirection, float bladeRadius, float sideDamping, float maxVelocity)
    {
        return _spawner != null && _spawner.ReleaseAtWorld(worldPoint, pressDirection, pressSpeed, outwardForce,
            tangentialForce, spinDirection, bladeRadius, sideDamping, maxVelocity);
    }
}
