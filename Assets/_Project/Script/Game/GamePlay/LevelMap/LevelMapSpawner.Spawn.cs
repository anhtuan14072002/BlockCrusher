using System.Collections.Generic;
using Crusher;
using Unity.Collections;
using UnityEngine;

public sealed partial class LevelMapSpawner
{
    [ContextMenu("Spawn")]
    public void Spawn()
    {
        Clear();
        if (_chunkMaterial == null)
        {
            Debug.LogError("LevelMapSpawner needs a chunk material.", this);
            return;
        }

        _runtimeParent = _container != null ? _container : transform;
        GameObject levelPrefab = GetCurrentLevelPrefab();
        if (levelPrefab == null)
        {
            Debug.LogError($"LevelMapSpawner needs a prefab for level {CurrentLevel}.", this);
            return;
        }

        GameObject level = Instantiate(levelPrefab, _runtimeParent, false);
        level.name = levelPrefab.name;
        BlockWater[] waterMarkers = level.GetComponentsInChildren<BlockWater>(true);
        LevelDecoration[] decorations = level.GetComponentsInChildren<LevelDecoration>(true);
        if (!TryBuildCellsFromLevel(level, waterMarkers))
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
        CreateDecorations(decorations);
        level.SetActive(false);
        DestroyUnityObject(level);
    }

    private bool TryBuildCellsFromLevel(GameObject level, BlockWater[] waterMarkers)
    {
        TypeBlockMap[] blocks = level.GetComponentsInChildren<TypeBlockMap>(true);
        MeshRenderer firstRenderer = null;
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, 0f);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, 0f);

        for (int i = 0; i < blocks.Length; i++)
        {
            firstRenderer ??= blocks[i].GetComponent<MeshRenderer>();
            IncludeGridPosition(blocks[i].transform.position, ref min, ref max);
        }

        for (int i = 0; i < waterMarkers.Length; i++)
            IncludeGridPosition(waterMarkers[i].transform.position, ref min, ref max);

        if (firstRenderer == null)
        {
            Debug.LogError("Level prefab has no cuttable blocks.", level);
            return false;
        }

        TypeBlockMap firstBlockMap = firstRenderer.GetComponent<TypeBlockMap>();
        _cellSize = GetCellSizeInSpace(firstBlockMap, _runtimeParent);
        if (_cellSize.x <= 0.0001f || _cellSize.y <= 0.0001f)
        {
            Debug.LogError("Level prefab blocks must use a non-zero cell size.", level);
            return false;
        }

        Vector3 blockSize = GetBlockSizeInSpace(firstRenderer, _runtimeParent);
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

        Vector2 alignmentTolerance = _cellSize * 0.01f;
        for (int i = 0; i < blocks.Length; i++)
        {
            TypeBlockMap blockMap = blocks[i];
            MeshRenderer renderer = blockMap.GetComponent<MeshRenderer>();
            int typeIndex = GetOrCreateReleasedBlockType(blockMap);
            if (typeIndex < 0)
            {
                DisposeCells();
                return false;
            }

            Vector3 localPosition = _runtimeParent.InverseTransformPoint(blockMap.transform.position);
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

            int cell = y * _gridWidth + x;
            if (_cellSolid[cell] != 0)
            {
                    Debug.LogError($"Level prefab has duplicate blocks at cell ({x}, {y}).", renderer);
                DisposeCells();
                return false;
            }

            _cellSolid[cell] = 1;
            _cellReleasedTypes[cell] = (ushort)typeIndex;
            _cellColors[cell] = blockMap.MapColor;
            _cellReleasedColors[cell] = blockMap.ReleasedColor;
        }

        BreakableObstacle[] breakableObstacles = level.GetComponentsInChildren<BreakableObstacle>(true);
        for (int i = 0; i < breakableObstacles.Length; i++)
        {
            if (RegisterBreakableObstacle(breakableObstacles[i]))
                continue;

            DisposeCells();
            return false;
        }

        BuildAuthoredMeshLibrary();
        return true;
    }

    private void IncludeGridPosition(Vector3 worldPosition, ref Vector3 min, ref Vector3 max)
    {
        Vector3 localPosition = _runtimeParent.InverseTransformPoint(worldPosition);
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
        float x = space.InverseTransformVector(
            renderer.transform.TransformVector(new Vector3(meshSize.x, 0f, 0f))).magnitude;
        float y = space.InverseTransformVector(
            renderer.transform.TransformVector(new Vector3(0f, meshSize.y, 0f))).magnitude;
        float z = space.InverseTransformVector(
            renderer.transform.TransformVector(new Vector3(0f, 0f, meshSize.z))).magnitude;
        return new Vector3(x, y, z);
    }

    private static Vector2 GetCellSizeInSpace(TypeBlockMap blockMap, Transform space)
    {
        if (blockMap == null)
            return Vector2.zero;

        float x = space.InverseTransformVector(
            blockMap.transform.TransformVector(Vector3.right * blockMap.CellSize)).magnitude;
        float y = space.InverseTransformVector(
            blockMap.transform.TransformVector(Vector3.up * blockMap.CellSize)).magnitude;
        return new Vector2(x, y);
    }

    [ContextMenu("Validate Level Prefab")]
    private void ValidateLevelPrefab()
    {
        GameObject levelPrefab = GetCurrentLevelPrefab();
        Debug.Assert(levelPrefab != null, $"LevelMapSpawner needs a prefab for level {CurrentLevel}.", this);
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

        int level = CurrentLevel > 0 ? CurrentLevel : _startLevel;
        return _levelPrefabs[Mathf.Clamp(level - 1, 0, _levelPrefabs.Length - 1)];
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
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
