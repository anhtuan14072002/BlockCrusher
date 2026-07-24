using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public sealed partial class TextureBlockSpawner
{
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
        LevelObstacle.MaskSpawnCells(_cellSolid, _runtimeParent, _offset, _gridWidth, _gridHeight, _cellSize);
        _runtimeChunkMaterial = ResolveChunkMaterial();
        SpawnMetaballWater();
        CreateChunks();
    }
    [ContextMenu("Clear")]
    public void Clear()
    {
        ClearMetaballWater();
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
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }
    private void DisposeCells()
    {
        if (_cellColors.IsCreated)
            _cellColors.Dispose();
        if (_cellSolid.IsCreated)
            _cellSolid.Dispose();
    }
}
