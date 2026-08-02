using UnityEngine;

public sealed partial class LevelMapSpawner
{
    private static readonly int CutMaskId = Shader.PropertyToID("_CutMask");
    private static readonly int CutGridId = Shader.PropertyToID("_CutGrid");
    private static readonly int CutMaskSizeId = Shader.PropertyToID("_CutMaskSize");
    private static readonly int CutEdgeId = Shader.PropertyToID("_CutEdge");
    private static readonly int CutRoughnessId = Shader.PropertyToID("_CutRoughness");

    private Texture2D _cutMaskTexture;
    private MaterialPropertyBlock _terrainPropertyBlock;
    private byte[] _cutMaskPixels;

    private void CreateCutMask()
    {
        DisposeCutMask();
        _cutMaskTexture = new Texture2D(_gridWidth, _gridHeight, TextureFormat.R8, false, true)
        {
            name = $"CutMask_{name}",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        _terrainPropertyBlock = new MaterialPropertyBlock();
        _cutMaskPixels = new byte[_cellSolid.Length];
        UpdateCutMask();
    }

    private void UpdateCutMask()
    {
        if (_cutMaskTexture == null || !_cellSolid.IsCreated)
            return;

        for (int i = 0; i < _cutMaskPixels.Length; i++)
            _cutMaskPixels[i] = _cellSolid[i] != 0 ? byte.MaxValue : (byte)0;
        _cutMaskTexture.SetPixelData(_cutMaskPixels, 0);
        _cutMaskTexture.Apply(false, false);
    }

    private void ApplyCutMask(Renderer renderer)
    {
        if (renderer == null || _cutMaskTexture == null)
            return;

        _terrainPropertyBlock.Clear();
        _terrainPropertyBlock.SetTexture(CutMaskId, _cutMaskTexture);
        _terrainPropertyBlock.SetVector(CutGridId,
            new Vector4(_offset.x, _offset.y, _cellSize, 1f));
        _terrainPropertyBlock.SetVector(CutMaskSizeId,
            new Vector4(_gridWidth, _gridHeight, 1f / _gridWidth, 1f / _gridHeight));
        _terrainPropertyBlock.SetFloat(CutEdgeId, 0.48f);
        _terrainPropertyBlock.SetFloat(CutRoughnessId, 0.22f);
        renderer.SetPropertyBlock(_terrainPropertyBlock);
    }

    private void DisposeCutMask()
    {
        if (_cutMaskTexture != null)
            DestroyUnityObject(_cutMaskTexture);
        _cutMaskTexture = null;
        _terrainPropertyBlock = null;
        _cutMaskPixels = null;
    }
}
