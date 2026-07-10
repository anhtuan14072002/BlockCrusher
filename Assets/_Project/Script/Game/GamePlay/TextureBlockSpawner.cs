using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
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
    [SerializeField] private GameObject _blockPrefab;
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
    [SerializeField, Min(0)] private int _prewarmReleasedBlockCount = 50;
    [SerializeField, Min(0.0001f)] private float _releasedBlockMass = 0.02f;
    [SerializeField] private bool _centerTexture = true;
    [SerializeField] private bool _spawnOnAwake = true;

    private readonly List<ChunkRuntime> _chunks = new();
    private readonly List<int> _dirtyChunks = new(16);
    private readonly List<int> _scheduledChunkRebuilds = new(8);
    private readonly List<JobHandle> _scheduledChunkHandles = new(8);
    private readonly Stack<ReleasedBlockRuntime> _releasedBlockPool = new(50);
    private readonly List<ReleasedBlockRuntime> _activeReleasedBlocks = new(64);
    private readonly Dictionary<PixelBlock, ReleasedBlockRuntime> _releasedBlockLookup = new(64);
    private NativeArray<Color32> _cellColors;
    private NativeArray<byte> _cellSolid;
    private NativeArray<Vector3> _collisionPreviousPositions;
    private NativeArray<Vector3> _collisionPositions;
    private NativeArray<Vector3> _collisionVelocities;
    private NativeArray<Vector2> _collisionHalfExtents;
    private NativeArray<byte> _collisionResolved;
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
        DisposeReleasedBlockPool();
        DisposeCollisionBuffers();
        DisposeCells();
    }

    private void FixedUpdate()
    {
        ResolveActiveReleasedBlockCollisions();
    }

    private void LateUpdate()
    {
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
        if (_texture == null || _blockPrefab == null)
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
        PrewarmReleasedBlockPool();
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
        ReleaseAllActiveBlocksToPool();

        Transform parent = _container != null ? _container : transform;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (Application.isPlaying && child.GetComponent<PixelBlock>() is PixelBlock pixelBlock &&
                _releasedBlockLookup.ContainsKey(pixelBlock))
                continue;

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

        Renderer prefabRenderer = _blockPrefab.GetComponentInChildren<Renderer>();
        return prefabRenderer != null ? prefabRenderer.sharedMaterial : null;
    }

    private Vector3 GetCellLocalPosition(int x, int y)
    {
        return _offset + new Vector3(x * _cellSize, y * _cellSize, 0f);
    }

    private void SpawnReleasedBlock(Vector3 localPosition, Color32 color, Vector3 sawCenter, Vector3 pressDirection,
        float pressSpeed, float outwardForce, float tangentialForce, float spinDirection, float bladeRadius,
        float sideDamping, float maxVelocity)
    {
        ReleasedBlockRuntime runtime = GetReleasedBlockRuntime();
        GameObject block = runtime.Block;
        block.name = "PixelBlock_Released";
        block.transform.localPosition = localPosition;
        block.transform.localRotation = Quaternion.identity;
        block.transform.localScale = Vector3.one * _cellSize;
        block.SetActive(true);

        PixelBlock pixelBlock = runtime.PixelBlock;
        ConfigureReleasedBlockVisual(runtime, color, localPosition);
        pixelBlock.SetMass(_releasedBlockMass);
        pixelBlock.Initialize(color, false);
        pixelBlock.Release();
        pixelBlock.ApplySawCompression(sawCenter, pressDirection, pressSpeed, block.transform.position, outwardForce,
            tangentialForce, spinDirection, bladeRadius, sideDamping, maxVelocity);
        runtime.PreviousWorldPosition = runtime.Rigidbody.position;
    }

    private void ConfigureReleasedBlockVisual(ReleasedBlockRuntime runtime, Color32 color, Vector3 localPosition)
    {
        if (runtime.Mesh == null || runtime.Renderer == null)
            return;

        int cellX = Mathf.RoundToInt((localPosition.x - _offset.x) / _cellSize);
        int cellY = Mathf.RoundToInt((localPosition.y - _offset.y) / _cellSize);
        UpdateReleasedBlockMesh(runtime, color, cellX, cellY);
        runtime.Renderer.sharedMaterial = _runtimeChunkMaterial != null ? _runtimeChunkMaterial : ResolveChunkMaterial();
        runtime.Renderer.SetPropertyBlock(null);
    }

    private void UpdateReleasedBlockMesh(ReleasedBlockRuntime runtime, Color32 color, int cellX, int cellY)
    {
        float halfDepth = _chunkColliderDepth / Mathf.Max(_cellSize, 0.0001f) * 0.5f;
        float half = _detailVoxelScale * 0.5f;
        float rotationY = GetVoxelRotationY(cellX, cellY, _voxelRandomYRotation);
        Vector3[] vertices = runtime.Vertices;
        Color32[] colors = runtime.Colors;

        Vector3 frontMin = new Vector3(-half, -half, -halfDepth);
        Vector3 frontMax = new Vector3(half, half, -halfDepth);
        Vector3 backMin = new Vector3(-half, -half, halfDepth);
        Vector3 backMax = new Vector3(half, half, halfDepth);

        FillFace(vertices, 0, rotationY,
            new Vector3(frontMin.x, frontMin.y, frontMin.z),
            new Vector3(frontMin.x, frontMax.y, frontMin.z),
            new Vector3(frontMax.x, frontMax.y, frontMin.z),
            new Vector3(frontMax.x, frontMin.y, frontMin.z));
        FillFace(vertices, 4, rotationY,
            new Vector3(backMax.x, backMin.y, backMax.z),
            new Vector3(backMax.x, backMax.y, backMax.z),
            new Vector3(backMin.x, backMax.y, backMax.z),
            new Vector3(backMin.x, backMin.y, backMax.z));
        FillFace(vertices, 8, rotationY,
            new Vector3(frontMin.x, frontMin.y, frontMin.z),
            new Vector3(backMin.x, backMin.y, backMin.z),
            new Vector3(backMin.x, backMax.y, backMax.z),
            new Vector3(frontMin.x, frontMax.y, frontMin.z));
        FillFace(vertices, 12, rotationY,
            new Vector3(frontMax.x, frontMin.y, frontMin.z),
            new Vector3(frontMax.x, frontMax.y, frontMin.z),
            new Vector3(backMax.x, backMax.y, backMax.z),
            new Vector3(backMax.x, backMin.y, backMax.z));
        FillFace(vertices, 16, rotationY,
            new Vector3(frontMin.x, frontMax.y, frontMin.z),
            new Vector3(backMin.x, backMax.y, backMax.z),
            new Vector3(backMax.x, backMax.y, backMax.z),
            new Vector3(frontMax.x, frontMax.y, frontMin.z));
        FillFace(vertices, 20, rotationY,
            new Vector3(frontMin.x, frontMin.y, frontMin.z),
            new Vector3(frontMax.x, frontMin.y, frontMin.z),
            new Vector3(backMax.x, backMin.y, backMax.z),
            new Vector3(backMin.x, backMin.y, backMin.z));

        for (int i = 0; i < colors.Length; i++)
            colors[i] = color;

        runtime.Mesh.vertices = vertices;
        runtime.Mesh.colors32 = colors;
        runtime.Mesh.RecalculateNormals();
        runtime.Mesh.RecalculateBounds();
    }

    internal void ReturnReleasedBlock(PixelBlock pixelBlock)
    {
        if (!_releasedBlockLookup.TryGetValue(pixelBlock, out ReleasedBlockRuntime runtime))
            return;

        if (_activeReleasedBlocks.Remove(runtime))
        {
            pixelBlock.RecycleToPool();
            runtime.Block.transform.SetParent(_runtimeParent, false);
            _releasedBlockPool.Push(runtime);
        }
    }

    private void PrewarmReleasedBlockPool()
    {
        if (!Application.isPlaying || _blockPrefab == null || _runtimeParent == null)
            return;

        while (_releasedBlockPool.Count + _activeReleasedBlocks.Count < _prewarmReleasedBlockCount)
            _releasedBlockPool.Push(CreateReleasedBlockRuntime());
    }

    private ReleasedBlockRuntime GetReleasedBlockRuntime()
    {
        ReleasedBlockRuntime runtime = _releasedBlockPool.Count > 0 ? _releasedBlockPool.Pop() : CreateReleasedBlockRuntime();
        runtime.Block.transform.SetParent(_runtimeParent, false);
        _activeReleasedBlocks.Add(runtime);
        return runtime;
    }

    private ReleasedBlockRuntime CreateReleasedBlockRuntime()
    {
        GameObject block = Instantiate(_blockPrefab, _runtimeParent);
        block.name = "PixelBlock_Released_Pooled";

        MeshFilter meshFilter = block.GetComponent<MeshFilter>();
        MeshRenderer meshRenderer = block.GetComponent<MeshRenderer>();
        PixelBlock pixelBlock = block.GetComponent<PixelBlock>();
        if (pixelBlock == null)
            pixelBlock = block.AddComponent<PixelBlock>();
        Rigidbody blockRigidbody = block.GetComponent<Rigidbody>();

        Vector3[] vertices = new Vector3[24];
        Color32[] colors = new Color32[24];
        Mesh mesh = null;
        if (meshFilter != null)
        {
            mesh = new Mesh { name = "PixelBlock_Released_Mesh" };
            mesh.MarkDynamic();
            mesh.vertices = vertices;
            mesh.colors32 = colors;
            mesh.uv = ReleasedBlockUvs;
            mesh.triangles = ReleasedBlockIndices;
            meshFilter.sharedMesh = mesh;
        }

        ReleasedBlockRuntime runtime = new ReleasedBlockRuntime
        {
            Block = block,
            Transform = block.transform,
            Rigidbody = blockRigidbody,
            PixelBlock = pixelBlock,
            Renderer = meshRenderer,
            Mesh = mesh,
            Vertices = vertices,
            Colors = colors
        };

        pixelBlock.SetPoolOwner(this);
        pixelBlock.SetMass(_releasedBlockMass);
        _releasedBlockLookup.Add(pixelBlock, runtime);
        pixelBlock.RecycleToPool();
        return runtime;
    }

    private void ResolveActiveReleasedBlockCollisions()
    {
        int blockCount = _activeReleasedBlocks.Count;
        if (blockCount == 0 || !_cellSolid.IsCreated || _runtimeParent == null)
            return;

        EnsureCollisionBufferCapacity(blockCount);

        for (int i = 0; i < blockCount; i++)
        {
            ReleasedBlockRuntime runtime = _activeReleasedBlocks[i];
            Rigidbody blockRigidbody = runtime.Rigidbody;
            Vector3 scale = runtime.Transform.lossyScale;

            _collisionPreviousPositions[i] = runtime.PreviousWorldPosition;
            _collisionPositions[i] = blockRigidbody.position;
            _collisionVelocities[i] = blockRigidbody.linearVelocity;
            _collisionHalfExtents[i] = new Vector2(Mathf.Abs(scale.x) * 0.5f, Mathf.Abs(scale.y) * 0.5f);
            _collisionResolved[i] = 0;
        }

        ResolveGridCollisionJob collisionJob = new ResolveGridCollisionJob
        {
            CellSolid = _cellSolid,
            PreviousWorldPositions = _collisionPreviousPositions,
            WorldPositions = _collisionPositions,
            Velocities = _collisionVelocities,
            HalfExtents = _collisionHalfExtents,
            Resolved = _collisionResolved,
            WorldToLocal = _runtimeParent.worldToLocalMatrix,
            LocalToWorld = _runtimeParent.localToWorldMatrix,
            GridWidth = _gridWidth,
            GridHeight = _gridHeight,
            CellSize = _cellSize,
            Offset = _offset
        };

        collisionJob.Schedule(blockCount, 64).Complete();

        for (int i = 0; i < blockCount; i++)
        {
            ReleasedBlockRuntime runtime = _activeReleasedBlocks[i];
            if (_collisionResolved[i] != 0)
            {
                runtime.Rigidbody.position = _collisionPositions[i];
                runtime.Rigidbody.linearVelocity = _collisionVelocities[i];
            }

            runtime.PreviousWorldPosition = _collisionPositions[i];
        }
    }

    private void EnsureCollisionBufferCapacity(int capacity)
    {
        if (_collisionPositions.IsCreated && _collisionPositions.Length >= capacity)
            return;

        DisposeCollisionBuffers();

        int bufferCapacity = Mathf.NextPowerOfTwo(capacity);
        _collisionPreviousPositions = new NativeArray<Vector3>(bufferCapacity, Allocator.Persistent);
        _collisionPositions = new NativeArray<Vector3>(bufferCapacity, Allocator.Persistent);
        _collisionVelocities = new NativeArray<Vector3>(bufferCapacity, Allocator.Persistent);
        _collisionHalfExtents = new NativeArray<Vector2>(bufferCapacity, Allocator.Persistent);
        _collisionResolved = new NativeArray<byte>(bufferCapacity, Allocator.Persistent);
    }

    private void ReleaseAllActiveBlocksToPool()
    {
        for (int i = _activeReleasedBlocks.Count - 1; i >= 0; i--)
        {
            ReleasedBlockRuntime runtime = _activeReleasedBlocks[i];
            runtime.PixelBlock.RecycleToPool();
            runtime.Block.transform.SetParent(_runtimeParent, false);
            _releasedBlockPool.Push(runtime);
        }

        _activeReleasedBlocks.Clear();
    }

    private void DisposeReleasedBlockPool()
    {
        foreach (ReleasedBlockRuntime runtime in _releasedBlockPool)
            DisposeReleasedBlockRuntime(runtime);

        for (int i = 0; i < _activeReleasedBlocks.Count; i++)
            DisposeReleasedBlockRuntime(_activeReleasedBlocks[i]);

        _releasedBlockPool.Clear();
        _activeReleasedBlocks.Clear();
        _releasedBlockLookup.Clear();
    }

    private static void DisposeReleasedBlockRuntime(ReleasedBlockRuntime runtime)
    {
        if (runtime.Mesh != null)
            DestroyUnityObject(runtime.Mesh);
    }

    private static void DestroyUnityObject(Object target)
    {
        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    private void DisposeCollisionBuffers()
    {
        if (_collisionPreviousPositions.IsCreated)
            _collisionPreviousPositions.Dispose();

        if (_collisionPositions.IsCreated)
            _collisionPositions.Dispose();

        if (_collisionVelocities.IsCreated)
            _collisionVelocities.Dispose();

        if (_collisionHalfExtents.IsCreated)
            _collisionHalfExtents.Dispose();

        if (_collisionResolved.IsCreated)
            _collisionResolved.Dispose();
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

    private sealed class ReleasedBlockRuntime
    {
        public GameObject Block;
        public Transform Transform;
        public Rigidbody Rigidbody;
        public PixelBlock PixelBlock;
        public MeshRenderer Renderer;
        public Mesh Mesh;
        public Vector3[] Vertices;
        public Color32[] Colors;
        public Vector3 PreviousWorldPosition;
    }

    [BurstCompile]
    private struct ResolveGridCollisionJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<byte> CellSolid;
        [ReadOnly] public NativeArray<Vector3> PreviousWorldPositions;
        public NativeArray<Vector3> WorldPositions;
        public NativeArray<Vector3> Velocities;
        [ReadOnly] public NativeArray<Vector2> HalfExtents;
        [WriteOnly] public NativeArray<byte> Resolved;
        public Matrix4x4 WorldToLocal;
        public Matrix4x4 LocalToWorld;
        public int GridWidth;
        public int GridHeight;
        public float CellSize;
        public Vector3 Offset;

        public void Execute(int index)
        {
            Vector3 worldPosition = WorldPositions[index];
            Vector3 velocity = Velocities[index];
            Vector2 halfExtents = HalfExtents[index];
            bool resolved = ResolveGridCollisionSwept(PreviousWorldPositions[index], ref worldPosition, ref velocity,
                halfExtents.x, halfExtents.y);

            WorldPositions[index] = worldPosition;
            Velocities[index] = velocity;
            Resolved[index] = resolved ? (byte)1 : (byte)0;
        }

        private bool ResolveGridCollisionSwept(Vector3 previousWorldPosition, ref Vector3 worldPosition,
            ref Vector3 velocity, float halfWidth, float halfHeight)
        {
            Vector3 fromLocal = TransformPoint(WorldToLocal, previousWorldPosition);
            Vector3 toLocal = TransformPoint(WorldToLocal, worldPosition);
            float dx = toLocal.x - fromLocal.x;
            float dy = toLocal.y - fromLocal.y;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            int steps = Mathf.Clamp(Mathf.CeilToInt(distance / Mathf.Max(CellSize * 0.35f, 0.0001f)), 1, 12);

            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector3 sampleWorldPosition = Vector3.Lerp(previousWorldPosition, worldPosition, t);

                if (!ResolveGridCollision(ref sampleWorldPosition, ref velocity, halfWidth, halfHeight))
                    continue;

                worldPosition = sampleWorldPosition;
                return true;
            }

            return false;
        }

        private bool ResolveGridCollision(ref Vector3 worldPosition, ref Vector3 velocity, float halfWidth,
            float halfHeight)
        {
            Vector3 localPosition = TransformPoint(WorldToLocal, worldPosition);
            float parentScaleX = Mathf.Max(GetColumnLength(LocalToWorld, 0), 0.0001f);
            float parentScaleY = Mathf.Max(GetColumnLength(LocalToWorld, 1), 0.0001f);
            float halfWidthLocal = halfWidth / parentScaleX;
            float halfHeightLocal = halfHeight / parentScaleY;
            float cellHalf = CellSize * 0.5f;
            int centerX = Mathf.RoundToInt((localPosition.x - Offset.x) / CellSize);
            int centerY = Mathf.RoundToInt((localPosition.y - Offset.y) / CellSize);
            int radiusX = Mathf.CeilToInt((halfWidthLocal + cellHalf) / CellSize) + 1;
            int radiusY = Mathf.CeilToInt((halfHeightLocal + cellHalf) / CellSize) + 1;
            int minX = Mathf.Max(0, centerX - radiusX);
            int maxX = Mathf.Min(GridWidth - 1, centerX + radiusX);
            int minY = Mathf.Max(0, centerY - radiusY);
            int maxY = Mathf.Min(GridHeight - 1, centerY + radiusY);
            bool resolved = false;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int cellIndex = y * GridWidth + x;
                    if (CellSolid[cellIndex] == 0)
                        continue;

                    Vector3 cellLocal = Offset + new Vector3(x * CellSize, y * CellSize, 0f);
                    float deltaX = localPosition.x - cellLocal.x;
                    float deltaY = localPosition.y - cellLocal.y;
                    float overlapX = halfWidthLocal + cellHalf - Mathf.Abs(deltaX);
                    float overlapY = halfHeightLocal + cellHalf - Mathf.Abs(deltaY);
                    if (overlapX <= 0f || overlapY <= 0f)
                        continue;

                    Vector3 localNormal;
                    if (overlapX < overlapY)
                    {
                        float sign = deltaX >= 0f ? 1f : -1f;
                        localPosition.x += overlapX * sign;
                        localNormal = new Vector3(sign, 0f, 0f);
                    }
                    else
                    {
                        float sign = deltaY >= 0f ? 1f : -1f;
                        localPosition.y += overlapY * sign;
                        localNormal = new Vector3(0f, sign, 0f);
                    }

                    Vector3 worldNormal = TransformVector(LocalToWorld, localNormal).normalized;
                    float normalVelocity = Vector3.Dot(velocity, worldNormal);
                    if (normalVelocity < 0f)
                        velocity -= worldNormal * normalVelocity;

                    resolved = true;
                }
            }

            if (resolved)
                worldPosition = TransformPoint(LocalToWorld, localPosition);

            return resolved;
        }

        private static Vector3 TransformPoint(Matrix4x4 matrix, Vector3 point)
        {
            return new Vector3(
                matrix.m00 * point.x + matrix.m01 * point.y + matrix.m02 * point.z + matrix.m03,
                matrix.m10 * point.x + matrix.m11 * point.y + matrix.m12 * point.z + matrix.m13,
                matrix.m20 * point.x + matrix.m21 * point.y + matrix.m22 * point.z + matrix.m23);
        }

        private static Vector3 TransformVector(Matrix4x4 matrix, Vector3 vector)
        {
            return new Vector3(
                matrix.m00 * vector.x + matrix.m01 * vector.y + matrix.m02 * vector.z,
                matrix.m10 * vector.x + matrix.m11 * vector.y + matrix.m12 * vector.z,
                matrix.m20 * vector.x + matrix.m21 * vector.y + matrix.m22 * vector.z);
        }

        private static float GetColumnLength(Matrix4x4 matrix, int column)
        {
            Vector3 columnVector = column == 0
                ? new Vector3(matrix.m00, matrix.m10, matrix.m20)
                : new Vector3(matrix.m01, matrix.m11, matrix.m21);

            return columnVector.magnitude;
        }
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
