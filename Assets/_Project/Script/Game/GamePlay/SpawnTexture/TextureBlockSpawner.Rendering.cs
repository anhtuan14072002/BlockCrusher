using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class TextureBlockSpawner
{
    private void DrawReleasedBlocks()
    {
        if (!EnsureEcsReady() || !EnsureReleasedBlockResources())
            return;
        ConsumePreparedRenderFrame();
        DrawCachedRenderFrame();
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
    internal bool TryCreateRenderPreparationJob(out PrepareRenderFrameJob job)
    {
        job = default;
        if (!_hasReleasedBlockQuery || _renderFrames[0] == null ||
            (_displayRenderFrame >= 0 && Time.frameCount % _releasedBlockRenderInterval != 0))
            return false;
        int writeIndex = _displayRenderFrame == 0 ? 1 : 0;
        RenderFrameData frame = _renderFrames[writeIndex];
        if (frame == null || frame.Pending)
            return false;
        frame.Count.Value = 0;
        job = new PrepareRenderFrameJob
        {
            Matrices = frame.Matrices,
            Colors = frame.Colors,
            Count = frame.Count,
            OwnerId = _ownerId
        };
        frame.Pending = true;
        _scheduledRenderFrame = writeIndex;
        return true;
    }
    internal void SetRenderPreparationHandle(JobHandle handle)
    {
        _renderFrames[_scheduledRenderFrame].Handle = handle;
        _scheduledRenderFrame = -1;
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
}
