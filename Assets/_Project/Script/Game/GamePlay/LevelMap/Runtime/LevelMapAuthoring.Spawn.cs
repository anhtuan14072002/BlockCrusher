using System.Collections.Generic;
using Crusher;
using Unity.Collections;
using UnityEngine;

public sealed partial class LevelMapAuthoring
{
    private const int TerrainFragmentColumns = 4;
    private const int TerrainFragmentRows = 3;
    private const int TerrainFragmentMeshCount = TerrainFragmentColumns * TerrainFragmentRows;

    [ContextMenu("Spawn")]
    public void Spawn()
    {
        Clear();
        if (_chunkMaterial == null)
        {
            Debug.LogError("LevelMapAuthoring needs a chunk material.", this);
            return;
        }

        _runtimeParent = _container != null ? _container : transform;
        GameObject levelPrefab = GetCurrentLevelPrefab();
        if (levelPrefab == null)
        {
            Debug.LogError($"LevelMapAuthoring needs a prefab for level {_currentLevel}.", this);
            return;
        }

        if (CanBuildDirectlyFromPrefab(levelPrefab))
        {
            if (!TryBuildCellsFromLevel(levelPrefab, System.Array.Empty<BlockWater>(), true))
                return;

            LevelObstacle.MaskSpawnCells(_cellSolid, _breakableCellMask, _runtimeParent, _offset,
                _gridWidth, _gridHeight, _cellSize);
            CreateCutParticles();
            _cellSolidSnapshot = _cellSolid.ToArray();
            CreateChunks();
            CreateDecorations(levelPrefab.GetComponentsInChildren<LevelDecoration>(true), levelPrefab.transform);
            return;
        }

        GameObject level = Instantiate(levelPrefab, _runtimeParent, false);
        level.name = levelPrefab.name;
        BlockWater[] waterMarkers = level.GetComponentsInChildren<BlockWater>(true);
        LevelDecoration[] decorations = level.GetComponentsInChildren<LevelDecoration>(true);
        if (!TryBuildCellsFromLevel(level, waterMarkers, false))
        {
            DestroyUnityObject(level);
            return;
        }

        LevelObstacle[] obstacles = level.GetComponentsInChildren<LevelObstacle>(true);
        for (int i = 0; i < obstacles.Length; i++)
        {
            obstacles[i].transform.SetParent(_runtimeParent, true);
            if (obstacles[i].TryGetComponent(out BreakableObstacle _))
            {
                Vector3 localPosition = obstacles[i].transform.localPosition;
                localPosition.z = -_chunkColliderDepth;
                obstacles[i].transform.localPosition = localPosition;
            }
        }

        LevelObstacle.MaskSpawnCells(_cellSolid, _breakableCellMask, _runtimeParent, _offset,
            _gridWidth, _gridHeight, _cellSize);
        CreateCutParticles();
        SpawnMetaballWater(waterMarkers);
        _cellSolidSnapshot = _cellSolid.ToArray();
        CreateChunks();
        CreateDecorations(decorations, _runtimeParent);
        level.SetActive(false);
        DestroyUnityObject(level);
    }

    private static bool CanBuildDirectlyFromPrefab(GameObject levelPrefab)
    {
        return levelPrefab.GetComponentInChildren<BlockWater>(true) == null &&
               levelPrefab.GetComponentInChildren<LevelObstacle>(true) == null;
    }

    private bool TryBuildCellsFromLevel(
        GameObject level, BlockWater[] waterMarkers, bool sourceIsPrefabAsset)
    {
        TypeBlockMap[] blocks = level.GetComponentsInChildren<TypeBlockMap>(true);
        MeshRenderer firstRenderer = null;
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, 0f);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, 0f);

        for (int i = 0; i < blocks.Length; i++)
        {
            firstRenderer ??= blocks[i].GetComponent<MeshRenderer>();
            IncludeGridPosition(blocks[i].transform.position, sourceIsPrefabAsset, ref min, ref max);
        }

        for (int i = 0; i < waterMarkers.Length; i++)
            IncludeGridPosition(waterMarkers[i].transform.position, sourceIsPrefabAsset, ref min, ref max);

        if (firstRenderer == null)
        {
            Debug.LogError("Level prefab has no cuttable blocks.", level);
            return false;
        }

        TypeBlockMap firstBlockMap = firstRenderer.GetComponent<TypeBlockMap>();
        _cellSize = GetCellSizeInSpace(firstBlockMap, sourceIsPrefabAsset ? null : _runtimeParent);
        if (_cellSize.x <= 0.0001f || _cellSize.y <= 0.0001f)
        {
            Debug.LogError("Level prefab blocks must use a non-zero cell size.", level);
            return false;
        }

        Vector3 blockSize = GetBlockSizeInSpace(firstRenderer, sourceIsPrefabAsset ? null : _runtimeParent);
        _chunkColliderDepth = blockSize.z;
        _offset = new Vector3(min.x, min.y, 0f);
        _gridWidth = Mathf.RoundToInt((max.x - min.x) / _cellSize.x) + 1;
        _gridHeight = Mathf.RoundToInt((max.y - min.y) / _cellSize.y) + 1;
        DisposeCells();
        _cellColors = new NativeArray<Color32>(_gridWidth * _gridHeight, Allocator.Persistent);
        _cellReleasedColors = new NativeArray<Color32>(_gridWidth * _gridHeight, Allocator.Persistent);
        _cellSolid = new NativeArray<byte>(_gridWidth * _gridHeight, Allocator.Persistent);
        _breakableCellMask = new NativeArray<byte>(_gridWidth * _gridHeight, Allocator.Persistent);
        _cellReleasedTypes = new NativeArray<ushort>(_gridWidth * _gridHeight, Allocator.Persistent);
        if (!HasValidTerrainFragmentMeshes())
        {
            Debug.LogError($"LevelMapAuthoring needs {TerrainFragmentMeshCount} terrain fragment meshes.", this);
            DisposeCells();
            return false;
        }

        Vector2 alignmentTolerance = _cellSize * 0.01f;
        for (int i = 0; i < blocks.Length; i++)
        {
            TypeBlockMap blockMap = blocks[i];
            MeshRenderer renderer = blockMap.GetComponent<MeshRenderer>();
            Vector3 localPosition = GetGridLocalPosition(blockMap.transform.position, sourceIsPrefabAsset);
            int x = Mathf.RoundToInt((localPosition.x - _offset.x) / _cellSize.x);
            int y = Mathf.RoundToInt((localPosition.y - _offset.y) / _cellSize.y);
            Vector3 cellPosition = GetCellLocalPosition(x, y);
            if (Mathf.Abs(localPosition.x - cellPosition.x) > alignmentTolerance.x ||
                Mathf.Abs(localPosition.y - cellPosition.y) > alignmentTolerance.y)
            {
                Debug.LogError($"Level block '{renderer.name}' is not aligned to the {_cellSize} grid.", renderer);
                DisposeCells();
                return false;
            }

            Mesh fragmentMesh = _terrainFragmentMeshes[GetTerrainFragmentMeshIndex(x, y)];
            int typeIndex = GetOrCreateReleasedBlockType(blockMap, fragmentMesh);
            if (typeIndex < 0)
            {
                DisposeCells();
                return false;
            }

            int cell = y * _gridWidth + x;
            if (_cellSolid[cell] != 0)
            {
                Debug.LogError($"Level prefab has duplicate blocks at cell ({x}, {y}).", renderer);
                DisposeCells();
                return false;
            }

            _cellSolid[cell] = 1;
            _cellReleasedTypes[cell] = (ushort)typeIndex;
            _cellColors[cell] = GetSurfaceColor(blockMap);
            _cellReleasedColors[cell] = blockMap.ReleasedColor;
        }

        BreakableObstacle[] breakableObstacles = level.GetComponentsInChildren<BreakableObstacle>(true);
        for (int i = 0; i < breakableObstacles.Length; i++)
            breakableObstacles[i].Initialize(this);

        BuildAuthoredMeshLibrary();
        return true;
    }

    private Color32 GetSurfaceColor(TypeBlockMap blockMap)
    {
        if (!_overrideDirtPalette || blockMap.BlockType != TypeBlock.Dirt)
            return blockMap.MapColor;

        // Dirt must read as one continuous surface. Variation is generated from world
        // position in the shader; per-cell tint would reveal the hidden destruction grid.
        Color soilColor = _soilBaseColor;
        // Alpha is an internal surface-class marker. The opaque shader restores output
        // alpha to one, so this does not make dirt transparent.
        soilColor.a = 0f;
        return soilColor;
    }

    private bool HasValidTerrainFragmentMeshes()
    {
        if (_terrainFragmentMeshes == null || _terrainFragmentMeshes.Length != TerrainFragmentMeshCount)
            return false;

        for (int i = 0; i < _terrainFragmentMeshes.Length; i++)
        {
            if (_terrainFragmentMeshes[i] == null)
                return false;
        }

        return true;
    }

    private static int GetTerrainFragmentMeshIndex(int x, int y)
    {
        int column = (x % TerrainFragmentColumns + TerrainFragmentColumns) % TerrainFragmentColumns;
        int row = (y % TerrainFragmentRows + TerrainFragmentRows) % TerrainFragmentRows;
        return row * TerrainFragmentColumns + column;
    }

    private bool TerrainFragmentMeshesShareEdges()
    {
        if (!HasValidTerrainFragmentMeshes())
            return false;

        int[] rightEdge = { 3, 4, 5, 6 };
        int[] leftEdge = { 0, 11, 10, 9 };
        int[] topEdge = { 9, 8, 7, 6 };
        int[] bottomEdge = { 0, 1, 2, 3 };
        const float tolerance = 0.000001f;

        for (int y = 0; y < TerrainFragmentRows; y++)
        {
            for (int x = 0; x < TerrainFragmentColumns; x++)
            {
                Vector3[] vertices = _terrainFragmentMeshes[GetTerrainFragmentMeshIndex(x, y)].vertices;
                Vector3[] rightVertices = _terrainFragmentMeshes[GetTerrainFragmentMeshIndex(x + 1, y)].vertices;
                Vector3[] topVertices = _terrainFragmentMeshes[GetTerrainFragmentMeshIndex(x, y + 1)].vertices;
                if (vertices.Length < 12 || rightVertices.Length < 12 || topVertices.Length < 12)
                    return false;

                for (int edgeVertex = 0; edgeVertex < 4; edgeVertex++)
                {
                    Vector3 rightPoint = vertices[rightEdge[edgeVertex]] + new Vector3(x, y, 0f);
                    Vector3 leftPoint = rightVertices[leftEdge[edgeVertex]] + new Vector3(x + 1, y, 0f);
                    Vector3 topPoint = vertices[topEdge[edgeVertex]] + new Vector3(x, y, 0f);
                    Vector3 bottomPoint = topVertices[bottomEdge[edgeVertex]] + new Vector3(x, y + 1, 0f);
                    if ((rightPoint - leftPoint).sqrMagnitude > tolerance * tolerance ||
                        (topPoint - bottomPoint).sqrMagnitude > tolerance * tolerance)
                        return false;
                }
            }
        }

        return true;
    }

    [ContextMenu("Validate Terrain Fragment Seams")]
    private void ValidateTerrainFragmentSeams()
    {
        Debug.Assert(TerrainFragmentMeshesShareEdges(),
            "Terrain fragment meshes do not share identical edges.", this);
    }

    private void IncludeGridPosition(
        Vector3 sourcePosition, bool sourceIsPrefabAsset, ref Vector3 min, ref Vector3 max)
    {
        Vector3 localPosition = GetGridLocalPosition(sourcePosition, sourceIsPrefabAsset);
        min.x = Mathf.Min(min.x, localPosition.x);
        min.y = Mathf.Min(min.y, localPosition.y);
        max.x = Mathf.Max(max.x, localPosition.x);
        max.y = Mathf.Max(max.y, localPosition.y);
    }

    private static Vector3 GetBlockSizeInSpace(MeshRenderer renderer, Transform space)
    {
        MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
            return Vector3.zero;

        Vector3 meshSize = meshFilter.sharedMesh.bounds.size;
        Vector3 worldX = renderer.transform.TransformVector(new Vector3(meshSize.x, 0f, 0f));
        Vector3 worldY = renderer.transform.TransformVector(new Vector3(0f, meshSize.y, 0f));
        Vector3 worldZ = renderer.transform.TransformVector(new Vector3(0f, 0f, meshSize.z));
        float x = (space != null ? space.InverseTransformVector(worldX) : worldX).magnitude;
        float y = (space != null ? space.InverseTransformVector(worldY) : worldY).magnitude;
        float z = (space != null ? space.InverseTransformVector(worldZ) : worldZ).magnitude;
        return new Vector3(x, y, z);
    }

    private static Vector2 GetCellSizeInSpace(TypeBlockMap blockMap, Transform space)
    {
        if (blockMap == null)
            return Vector2.zero;

        Vector3 worldX = blockMap.transform.TransformVector(Vector3.right * blockMap.CellSize);
        Vector3 worldY = blockMap.transform.TransformVector(Vector3.up * blockMap.CellSize);
        float x = (space != null ? space.InverseTransformVector(worldX) : worldX).magnitude;
        float y = (space != null ? space.InverseTransformVector(worldY) : worldY).magnitude;
        return new Vector2(x, y);
    }

    private Vector3 GetGridLocalPosition(Vector3 sourcePosition, bool sourceIsPrefabAsset)
    {
        return sourceIsPrefabAsset ? sourcePosition : _runtimeParent.InverseTransformPoint(sourcePosition);
    }

    [ContextMenu("Validate Level Prefab")]
    private void ValidateLevelPrefab()
    {
        GameObject levelPrefab = GetCurrentLevelPrefab();
        Debug.Assert(levelPrefab != null, $"LevelMapAuthoring needs a prefab for level {_currentLevel}.", this);
        if (levelPrefab == null)
            return;

        TypeBlockMap[] blocks = levelPrefab.GetComponentsInChildren<TypeBlockMap>(true);
        MeshRenderer firstBlock = blocks.Length > 0 ? blocks[0].GetComponent<MeshRenderer>() : null;

        Debug.Assert(firstBlock != null, "Level prefab has no cuttable blocks.", levelPrefab);
        if (firstBlock == null)
            return;

        Vector2 cellSize = GetCellSizeInSpace(blocks[0], levelPrefab.transform);
        Debug.Assert(cellSize.x > 0.0001f && cellSize.y > 0.0001f,
            "Level prefab block size is invalid.", firstBlock);
        if (cellSize.x <= 0.0001f || cellSize.y <= 0.0001f)
            return;

        Debug.Assert(HasValidTerrainFragmentMeshes(),
            $"LevelMapAuthoring needs {TerrainFragmentMeshCount} terrain fragment meshes.", this);
        Debug.Assert(GetTerrainFragmentMeshIndex(0, 0) == 0 &&
                     GetTerrainFragmentMeshIndex(3, 2) == 11 &&
                     GetTerrainFragmentMeshIndex(4, 3) == 0 &&
                     GetTerrainFragmentMeshIndex(-1, -1) == 11,
            "Terrain fragment mosaic indexing failed.", this);
        Debug.Assert(TerrainFragmentMeshesShareEdges(),
            "Terrain fragment meshes do not share identical edges.", this);

        HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();
        for (int i = 0; i < blocks.Length; i++)
        {
            TypeBlockMap blockMap = blocks[i];
            MeshRenderer renderer = blockMap.GetComponent<MeshRenderer>();
            Debug.Assert(blockMap.ReleasedPrefab != null, $"Level block '{blockMap.name}' needs a released prefab.", blockMap);
            Debug.Assert(blockMap.BlockType != TypeBlock.None,
                $"Level block '{blockMap.name}' needs a collectible id.", blockMap);
            Debug.Assert(blockMap.ReleasedScale > 0f, $"Level block '{blockMap.name}' needs a positive released scale.", blockMap);
            Debug.Assert(blockMap.CellSize > 0f, $"Level block '{blockMap.name}' needs a positive cell size.", blockMap);

            Vector3 localPosition = levelPrefab.transform.InverseTransformPoint(blockMap.transform.position);
            Vector2Int cell = new Vector2Int(
                Mathf.RoundToInt(localPosition.x / cellSize.x),
                Mathf.RoundToInt(localPosition.y / cellSize.y));
            Debug.Assert(occupiedCells.Add(cell), $"Duplicate level block at cell {cell}.", renderer);
            Debug.Assert(Mathf.Abs(localPosition.x - cell.x * cellSize.x) <= cellSize.x * 0.01f &&
                         Mathf.Abs(localPosition.y - cell.y * cellSize.y) <= cellSize.y * 0.01f,
                $"Level block '{renderer.name}' is not aligned to the {cellSize} grid.", renderer);
        }

        Debug.Log($"Level prefab validation passed: {occupiedCells.Count} cuttable blocks, cell size {cellSize}.",
            levelPrefab);
    }

    private GameObject GetCurrentLevelPrefab()
    {
        if (_levelPrefabs == null || _levelPrefabs.Length == 0)
            return null;

        int level = _currentLevel > 0 ? _currentLevel : _startLevel;
        return _levelPrefabs[Mathf.Clamp(level - 1, 0, _levelPrefabs.Length - 1)];
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        _cutDebris?.ResetDebris();
        CompleteReleasedBlockJobs();
        ClearMetaballWater();
        DisposeDecorations();
        DisposeCutParticles();
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
            if (_metaballParticles != null && child == _metaballParticles.transform)
                continue;

            child.gameObject.SetActive(false);
            DestroyUnityObject(child.gameObject);
        }
    }

    private void DisposeCells()
    {
        DisposeAuthoredMeshLibrary();
        if (_cellColors.IsCreated)
            _cellColors.Dispose();
        if (_cellReleasedColors.IsCreated)
            _cellReleasedColors.Dispose();
        if (_cellSolid.IsCreated)
            _cellSolid.Dispose();
        if (_breakableCellMask.IsCreated)
            _breakableCellMask.Dispose();
        _cellSolidSnapshot = null;
        if (_cellReleasedTypes.IsCreated)
            _cellReleasedTypes.Dispose();
    }
}
