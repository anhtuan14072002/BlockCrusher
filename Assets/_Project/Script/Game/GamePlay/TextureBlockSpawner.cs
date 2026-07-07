using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class TextureBlockSpawner : MonoBehaviour
{
    private static readonly List<TextureBlockSpawner> ActiveSpawners = new();

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
    [SerializeField] private bool _centerTexture = true;
    [SerializeField] private bool _spawnOnAwake = true;

    private readonly List<ChunkRuntime> _chunks = new();
    private NativeArray<Color32> _cellColors;
    private NativeArray<byte> _cellSolid;
    private Transform _runtimeParent;
    private Material _runtimeChunkMaterial;
    private Vector3 _offset;
    private int _gridWidth;
    private int _gridHeight;
    private float _cellSize;

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

    public static void ResolveGridCollisionForActiveSpawners(Transform blockTransform, Rigidbody blockRigidbody,
        Vector3 previousWorldPosition)
    {
        Vector3 position = blockRigidbody.position;
        Vector3 velocity = blockRigidbody.linearVelocity;
        Vector3 scale = blockTransform.lossyScale;
        float halfWidth = Mathf.Abs(scale.x) * 0.5f;
        float halfHeight = Mathf.Abs(scale.y) * 0.5f;
        bool resolved = false;

        for (int i = ActiveSpawners.Count - 1; i >= 0; i--)
        {
            TextureBlockSpawner spawner = ActiveSpawners[i];
            if (spawner == null)
            {
                ActiveSpawners.RemoveAt(i);
                continue;
            }

            resolved |= spawner.ResolveGridCollisionSwept(previousWorldPosition, ref position, ref velocity, halfWidth,
                halfHeight);
        }

        if (!resolved)
            return;

        blockRigidbody.position = position;
        blockRigidbody.linearVelocity = velocity;
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
        DisposeCells();
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
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        DisposeCells();
        _chunks.Clear();

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
                SpawnReleasedBlock(cellLocal, color, worldPoint, pressDirection, pressSpeed, outwardForce,
                    tangentialForce, spinDirection, bladeRadius, sideDamping, maxVelocity);
                releasedAny = true;
            }
        }

        if (releasedAny && _chunks.Count > 0)
            RebuildChunk(0);

        return releasedAny;
    }

    private void CreateChunks()
    {
        Material material = _runtimeChunkMaterial;

        GameObject chunkObject = new GameObject("TextureChunk");
        chunkObject.transform.SetParent(_runtimeParent, false);

        MeshFilter meshFilter = chunkObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = chunkObject.AddComponent<MeshRenderer>();
        TextureBlockChunk chunk = chunkObject.AddComponent<TextureBlockChunk>();
        Mesh mesh = new Mesh { name = "TextureChunk_Mesh" };
        mesh.MarkDynamic();

        meshFilter.sharedMesh = mesh;
        meshRenderer.sharedMaterial = material;
        chunk.Initialize(this);

        _chunks.Add(new ChunkRuntime
        {
            Mesh = mesh,
            Renderer = meshRenderer,
            StartX = 0,
            StartY = 0,
            Width = _gridWidth,
            Height = _gridHeight,
            IsDetailed = _renderVoxelDetailFromStart
        });

        RebuildChunk(0);
    }

    private void RebuildChunk(int chunkIndex)
    {
        ChunkRuntime chunk = _chunks[chunkIndex];
        int maxCells = chunk.Width * chunk.Height;
        int vertexCapacity = chunk.IsDetailed ? maxCells * 24 : maxCells * 4;
        int indexCapacity = chunk.IsDetailed ? maxCells * 36 : maxCells * 6;
        NativeArray<byte> visited = new NativeArray<byte>(maxCells, Allocator.TempJob);
        NativeList<Vector3> vertices = new NativeList<Vector3>(vertexCapacity, Allocator.TempJob);
        NativeList<Color32> colors = new NativeList<Color32>(vertexCapacity, Allocator.TempJob);
        NativeList<Vector2> uvs = new NativeList<Vector2>(vertexCapacity, Allocator.TempJob);
        NativeList<int> indices = new NativeList<int>(indexCapacity, Allocator.TempJob);

        BuildChunkMeshJob meshJob = new BuildChunkMeshJob
        {
            CellColors = _cellColors,
            CellSolid = _cellSolid,
            Visited = visited,
            Vertices = vertices,
            Colors = colors,
            Uvs = uvs,
            Indices = indices,
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

        meshJob.Schedule().Complete();

        Mesh mesh = chunk.Mesh;
        mesh.Clear();

        if (vertices.Length > 0)
        {
            mesh.indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices.AsArray());
            mesh.SetColors(colors.AsArray());
            mesh.SetUVs(0, uvs.AsArray());
            mesh.SetIndices(indices.AsArray(), MeshTopology.Triangles, 0);
            if (chunk.IsDetailed)
                mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            chunk.Renderer.enabled = true;
        }
        else
        {
            chunk.Renderer.enabled = false;
        }

        vertices.Dispose();
        colors.Dispose();
        uvs.Dispose();
        indices.Dispose();
        visited.Dispose();
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
        GameObject block = Instantiate(_blockPrefab, _runtimeParent);
        block.name = "PixelBlock_Released";
        block.transform.localPosition = localPosition;
        block.transform.localRotation = Quaternion.identity;
        block.transform.localScale = Vector3.one * _cellSize;
        ConfigureReleasedBlockVisual(block, color, localPosition);

        PixelBlock pixelBlock = block.GetComponent<PixelBlock>();
        if (pixelBlock == null)
            pixelBlock = block.AddComponent<PixelBlock>();

        pixelBlock.Initialize(color, false);
        pixelBlock.Release();
        pixelBlock.ApplySawCompression(sawCenter, pressDirection, pressSpeed, block.transform.position, outwardForce,
            tangentialForce, spinDirection, bladeRadius, sideDamping, maxVelocity);
    }

    private void ConfigureReleasedBlockVisual(GameObject block, Color32 color, Vector3 localPosition)
    {
        MeshFilter meshFilter = block.GetComponent<MeshFilter>();
        MeshRenderer meshRenderer = block.GetComponent<MeshRenderer>();
        if (meshFilter == null || meshRenderer == null)
            return;

        int cellX = Mathf.RoundToInt((localPosition.x - _offset.x) / _cellSize);
        int cellY = Mathf.RoundToInt((localPosition.y - _offset.y) / _cellSize);
        meshFilter.sharedMesh = CreateReleasedBlockMesh(color, cellX, cellY);
        meshRenderer.sharedMaterial = _runtimeChunkMaterial != null ? _runtimeChunkMaterial : ResolveChunkMaterial();
        meshRenderer.SetPropertyBlock(null);
    }

    private Mesh CreateReleasedBlockMesh(Color32 color, int cellX, int cellY)
    {
        float halfDepth = _chunkColliderDepth / Mathf.Max(_cellSize, 0.0001f) * 0.5f;
        float half = _detailVoxelScale * 0.5f;
        float rotationY = GetVoxelRotationY(cellX, cellY, _voxelRandomYRotation);

        Mesh mesh = new Mesh { name = "PixelBlock_Released_Mesh" };
        Vector3[] vertices = new Vector3[24];
        Color32[] colors = new Color32[24];
        Vector2[] uvs = new Vector2[24];
        int[] indices =
        {
            0, 1, 2, 0, 2, 3,
            4, 5, 6, 4, 6, 7,
            8, 9, 10, 8, 10, 11,
            12, 13, 14, 12, 14, 15,
            16, 17, 18, 16, 18, 19,
            20, 21, 22, 20, 22, 23
        };

        Vector3 frontMin = new Vector3(-half, -half, -halfDepth);
        Vector3 frontMax = new Vector3(half, half, -halfDepth);
        Vector3 backMin = new Vector3(-half, -half, halfDepth);
        Vector3 backMax = new Vector3(half, half, halfDepth);

        FillFace(vertices, uvs, 0, rotationY,
            new Vector3(frontMin.x, frontMin.y, frontMin.z),
            new Vector3(frontMin.x, frontMax.y, frontMin.z),
            new Vector3(frontMax.x, frontMax.y, frontMin.z),
            new Vector3(frontMax.x, frontMin.y, frontMin.z));
        FillFace(vertices, uvs, 4, rotationY,
            new Vector3(backMax.x, backMin.y, backMax.z),
            new Vector3(backMax.x, backMax.y, backMax.z),
            new Vector3(backMin.x, backMax.y, backMax.z),
            new Vector3(backMin.x, backMin.y, backMax.z));
        FillFace(vertices, uvs, 8, rotationY,
            new Vector3(frontMin.x, frontMin.y, frontMin.z),
            new Vector3(backMin.x, backMin.y, backMin.z),
            new Vector3(backMin.x, backMax.y, backMax.z),
            new Vector3(frontMin.x, frontMax.y, frontMin.z));
        FillFace(vertices, uvs, 12, rotationY,
            new Vector3(frontMax.x, frontMin.y, frontMin.z),
            new Vector3(frontMax.x, frontMax.y, frontMin.z),
            new Vector3(backMax.x, backMax.y, backMax.z),
            new Vector3(backMax.x, backMin.y, backMax.z));
        FillFace(vertices, uvs, 16, rotationY,
            new Vector3(frontMin.x, frontMax.y, frontMin.z),
            new Vector3(backMin.x, backMax.y, backMax.z),
            new Vector3(backMax.x, backMax.y, backMax.z),
            new Vector3(frontMax.x, frontMax.y, frontMin.z));
        FillFace(vertices, uvs, 20, rotationY,
            new Vector3(frontMin.x, frontMin.y, frontMin.z),
            new Vector3(frontMax.x, frontMin.y, frontMin.z),
            new Vector3(backMax.x, backMin.y, backMax.z),
            new Vector3(backMin.x, backMin.y, backMin.z));

        for (int i = 0; i < colors.Length; i++)
            colors[i] = color;

        mesh.vertices = vertices;
        mesh.colors32 = colors;
        mesh.uv = uvs;
        mesh.triangles = indices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void FillFace(Vector3[] vertices, Vector2[] uvs, int startIndex, float rotationY, Vector3 a,
        Vector3 b, Vector3 c, Vector3 d)
    {
        vertices[startIndex] = RotateY(a, rotationY);
        vertices[startIndex + 1] = RotateY(b, rotationY);
        vertices[startIndex + 2] = RotateY(c, rotationY);
        vertices[startIndex + 3] = RotateY(d, rotationY);

        uvs[startIndex] = new Vector2(0f, 0f);
        uvs[startIndex + 1] = new Vector2(0f, 1f);
        uvs[startIndex + 2] = new Vector2(1f, 1f);
        uvs[startIndex + 3] = new Vector2(1f, 0f);
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

    private bool ResolveGridCollisionSwept(Vector3 previousWorldPosition, ref Vector3 worldPosition,
        ref Vector3 velocity, float halfWidth, float halfHeight)
    {
        if (!_cellSolid.IsCreated || _runtimeParent == null)
            return false;

        Vector3 fromLocal = _runtimeParent.InverseTransformPoint(previousWorldPosition);
        Vector3 toLocal = _runtimeParent.InverseTransformPoint(worldPosition);
        float distance = Vector2.Distance(new Vector2(fromLocal.x, fromLocal.y), new Vector2(toLocal.x, toLocal.y));
        int steps = Mathf.Clamp(Mathf.CeilToInt(distance / Mathf.Max(_cellSize * 0.35f, 0.0001f)), 1, 12);

        bool resolved = false;

        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector3 sampleWorldPosition = Vector3.Lerp(previousWorldPosition, worldPosition, t);

            if (!ResolveGridCollision(ref sampleWorldPosition, ref velocity, halfWidth, halfHeight))
                continue;

            worldPosition = sampleWorldPosition;
            resolved = true;
            break;
        }

        return resolved;
    }

    private bool ResolveGridCollision(ref Vector3 worldPosition, ref Vector3 velocity, float halfWidth,
        float halfHeight)
    {
        if (!_cellSolid.IsCreated || _runtimeParent == null)
            return false;

        Vector3 localPosition = _runtimeParent.InverseTransformPoint(worldPosition);
        Vector3 parentScale = _runtimeParent.lossyScale;
        float halfWidthLocal = halfWidth / Mathf.Max(Mathf.Abs(parentScale.x), 0.0001f);
        float halfHeightLocal = halfHeight / Mathf.Max(Mathf.Abs(parentScale.y), 0.0001f);
        float cellHalf = _cellSize * 0.5f;
        int centerX = Mathf.RoundToInt((localPosition.x - _offset.x) / _cellSize);
        int centerY = Mathf.RoundToInt((localPosition.y - _offset.y) / _cellSize);
        int radiusX = Mathf.CeilToInt((halfWidthLocal + cellHalf) / _cellSize) + 1;
        int radiusY = Mathf.CeilToInt((halfHeightLocal + cellHalf) / _cellSize) + 1;
        int minX = Mathf.Max(0, centerX - radiusX);
        int maxX = Mathf.Min(_gridWidth - 1, centerX + radiusX);
        int minY = Mathf.Max(0, centerY - radiusY);
        int maxY = Mathf.Min(_gridHeight - 1, centerY + radiusY);
        bool resolved = false;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int cellIndex = y * _gridWidth + x;
                if (_cellSolid[cellIndex] == 0)
                    continue;

                Vector3 cellLocal = GetCellLocalPosition(x, y);
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

                Vector3 worldNormal = _runtimeParent.TransformDirection(localNormal).normalized;
                float normalVelocity = Vector3.Dot(velocity, worldNormal);
                if (normalVelocity < 0f)
                    velocity -= worldNormal * normalVelocity;

                resolved = true;
            }
        }

        if (resolved)
            worldPosition = _runtimeParent.TransformPoint(localPosition);

        return resolved;
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