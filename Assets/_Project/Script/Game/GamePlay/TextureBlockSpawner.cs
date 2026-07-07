using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class TextureBlockSpawner : MonoBehaviour
{
    [SerializeField] private Texture2D _texture;
    [SerializeField] private GameObject _blockPrefab;
    [SerializeField] private Transform _container;
    [SerializeField] private Material _chunkMaterial;
    [SerializeField] private float _pixelSize = 0.12f;
    [SerializeField, Range(1, 16)] private int _sampleStep = 1;
    [SerializeField, Range(4, 64)] private int _chunkSize = 32;
    [SerializeField, Range(0f, 1f)] private float _alphaThreshold = 0.1f;
    [SerializeField] private float _chunkColliderDepth = 0.25f;
    [SerializeField] private float _sawReleaseRadius = 0.18f;
    [SerializeField] private bool _centerTexture = true;
    [SerializeField] private bool _applyPixelColor = true;
    [SerializeField] private bool _spawnOnAwake = true;

    private readonly List<ChunkRuntime> _chunks = new();
    private NativeArray<Color32> _cellColors;
    private NativeArray<byte> _cellSolid;
    private Transform _runtimeParent;
    private Vector3 _offset;
    private int _gridWidth;
    private int _gridHeight;
    private float _cellSize;

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
        _offset = _centerTexture ? new Vector3((_gridWidth - 1) * _cellSize * -0.5f, (_gridHeight - 1) * _cellSize * -0.5f, 0f) : Vector3.zero;

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

    public void ReleaseAtWorld(Vector3 worldPoint, Vector3 pressDirection, float pressSpeed, float force, float sideDamping, float maxVelocity)
    {
        if (!_cellSolid.IsCreated || _runtimeParent == null)
            return;

        Vector3 localPoint = _runtimeParent.InverseTransformPoint(worldPoint);
        float radiusSqr = _sawReleaseRadius * _sawReleaseRadius;
        int centerX = Mathf.RoundToInt((localPoint.x - _offset.x) / _cellSize);
        int centerY = Mathf.RoundToInt((localPoint.y - _offset.y) / _cellSize);
        int radiusCells = Mathf.Max(1, Mathf.CeilToInt(_sawReleaseRadius / _cellSize));
        int minX = Mathf.Max(0, centerX - radiusCells);
        int maxX = Mathf.Min(_gridWidth - 1, centerX + radiusCells);
        int minY = Mathf.Max(0, centerY - radiusCells);
        int maxY = Mathf.Min(_gridHeight - 1, centerY + radiusCells);

        bool[] dirtyChunks = new bool[_chunks.Count];
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
                SpawnReleasedBlock(cellLocal, color, worldPoint, pressDirection, pressSpeed, force, sideDamping, maxVelocity);
                MarkDirtyChunk(x, y, dirtyChunks);
                releasedAny = true;
            }
        }

        if (!releasedAny)
            return;

        for (int i = 0; i < dirtyChunks.Length; i++)
        {
            if (dirtyChunks[i])
                RebuildChunk(i);
        }
    }

    private void CreateChunks()
    {
        int chunkColumns = Mathf.CeilToInt(_gridWidth / (float)_chunkSize);
        int chunkRows = Mathf.CeilToInt(_gridHeight / (float)_chunkSize);
        Material material = ResolveChunkMaterial();

        for (int chunkY = 0; chunkY < chunkRows; chunkY++)
        {
            for (int chunkX = 0; chunkX < chunkColumns; chunkX++)
            {
                int startX = chunkX * _chunkSize;
                int startY = chunkY * _chunkSize;
                int width = Mathf.Min(_chunkSize, _gridWidth - startX);
                int height = Mathf.Min(_chunkSize, _gridHeight - startY);

                GameObject chunkObject = new GameObject("TextureChunk_" + chunkX + "_" + chunkY);
                chunkObject.transform.SetParent(_runtimeParent, false);

                MeshFilter meshFilter = chunkObject.AddComponent<MeshFilter>();
                MeshRenderer meshRenderer = chunkObject.AddComponent<MeshRenderer>();
                BoxCollider collider = chunkObject.AddComponent<BoxCollider>();
                Rigidbody rigidbody = chunkObject.AddComponent<Rigidbody>();
                TextureBlockChunk chunk = chunkObject.AddComponent<TextureBlockChunk>();
                Mesh mesh = new Mesh { name = chunkObject.name + "_Mesh" };
                mesh.MarkDynamic();

                meshFilter.sharedMesh = mesh;
                meshRenderer.sharedMaterial = material;
                collider.isTrigger = true;
                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;
                rigidbody.constraints = RigidbodyConstraints.FreezeAll;
                ConfigureChunkCollider(collider, startX, startY, width, height);
                chunk.Initialize(this);

                _chunks.Add(new ChunkRuntime
                {
                    Mesh = mesh,
                    Renderer = meshRenderer,
                    Collider = collider,
                    StartX = startX,
                    StartY = startY,
                    Width = width,
                    Height = height
                });

                RebuildChunk(_chunks.Count - 1);
            }
        }
    }

    private void RebuildChunk(int chunkIndex)
    {
        ChunkRuntime chunk = _chunks[chunkIndex];
        int maxCells = chunk.Width * chunk.Height;
        NativeArray<byte> visited = new NativeArray<byte>(maxCells, Allocator.TempJob);
        NativeList<Vector3> vertices = new NativeList<Vector3>(maxCells * 4, Allocator.TempJob);
        NativeList<Color32> colors = new NativeList<Color32>(maxCells * 4, Allocator.TempJob);
        NativeList<int> indices = new NativeList<int>(maxCells * 6, Allocator.TempJob);

        BuildChunkMeshJob meshJob = new BuildChunkMeshJob
        {
            CellColors = _cellColors,
            CellSolid = _cellSolid,
            Visited = visited,
            Vertices = vertices,
            Colors = colors,
            Indices = indices,
            GridWidth = _gridWidth,
            StartX = chunk.StartX,
            StartY = chunk.StartY,
            ChunkWidth = chunk.Width,
            ChunkHeight = chunk.Height,
            CellSize = _cellSize,
            Offset = _offset
        };

        meshJob.Schedule().Complete();

        Mesh mesh = chunk.Mesh;
        mesh.Clear();

        if (vertices.Length > 0)
        {
            mesh.indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices.AsArray());
            mesh.SetColors(colors.AsArray());
            mesh.SetIndices(indices.AsArray(), MeshTopology.Triangles, 0);
            mesh.RecalculateBounds();
            chunk.Renderer.enabled = true;
            chunk.Collider.enabled = true;
        }
        else
        {
            chunk.Renderer.enabled = false;
            chunk.Collider.enabled = false;
        }

        vertices.Dispose();
        colors.Dispose();
        indices.Dispose();
        visited.Dispose();
    }

    private void ConfigureChunkCollider(BoxCollider collider, int startX, int startY, int width, int height)
    {
        Vector3 min = GetCellLocalPosition(startX, startY) - new Vector3(_cellSize, _cellSize, _chunkColliderDepth) * 0.5f;
        Vector3 max = GetCellLocalPosition(startX + width - 1, startY + height - 1) + new Vector3(_cellSize, _cellSize, _chunkColliderDepth) * 0.5f;
        collider.center = (min + max) * 0.5f;
        collider.size = max - min;
    }

    private Material ResolveChunkMaterial()
    {
        if (_chunkMaterial != null)
            return _chunkMaterial;

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

    private void SpawnReleasedBlock(Vector3 localPosition, Color32 color, Vector3 contactPoint, Vector3 pressDirection, float pressSpeed, float force, float sideDamping, float maxVelocity)
    {
        GameObject block = Instantiate(_blockPrefab, _runtimeParent);
        block.name = "PixelBlock_Released";
        block.transform.localPosition = localPosition;
        block.transform.localRotation = Quaternion.identity;
        block.transform.localScale = Vector3.one * _cellSize;

        PixelBlock pixelBlock = block.GetComponent<PixelBlock>();
        if (pixelBlock == null)
            pixelBlock = block.AddComponent<PixelBlock>();

        pixelBlock.Initialize(color, _applyPixelColor);
        pixelBlock.Release();
        pixelBlock.ApplySawCompression(pressDirection, pressSpeed, contactPoint, force, sideDamping, maxVelocity);
    }

    private void MarkDirtyChunk(int cellX, int cellY, bool[] dirtyChunks)
    {
        int chunkX = cellX / _chunkSize;
        int chunkY = cellY / _chunkSize;
        int chunkColumns = Mathf.CeilToInt(_gridWidth / (float)_chunkSize);
        int chunkIndex = chunkY * chunkColumns + chunkX;
        if ((uint)chunkIndex < (uint)dirtyChunks.Length)
            dirtyChunks[chunkIndex] = true;
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
        public BoxCollider Collider;
        public int StartX;
        public int StartY;
        public int Width;
        public int Height;
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
            CellColors[index] = color;
            CellSolid[index] = color.a > AlphaLimit ? (byte)1 : (byte)0;
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
        public NativeList<int> Indices;
        public int GridWidth;
        public int StartX;
        public int StartY;
        public int ChunkWidth;
        public int ChunkHeight;
        public float CellSize;
        public Vector3 Offset;

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

    public void ReleaseAtWorld(Vector3 worldPoint, Vector3 pressDirection, float pressSpeed, float force, float sideDamping, float maxVelocity)
    {
        _spawner?.ReleaseAtWorld(worldPoint, pressDirection, pressSpeed, force, sideDamping, maxVelocity);
    }
}
