using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class LevelMapAuthoring
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
            Types = frame.Types,
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
        for (int i = 0; i < _releasedBlockTypes.Count; i++)
        {
            _releasedBlockTypes[i].RenderCount = 0;
            _releasedBlockTypes[i].BatchCount = 0;
        }

        for (int sourceIndex = 0; sourceIndex < count; sourceIndex++)
        {
            ushort typeIndex = frame.Types[sourceIndex];
            if (typeIndex >= _releasedBlockTypes.Count)
                continue;

            ReleasedBlockRuntimeType runtimeType = _releasedBlockTypes[typeIndex];
            int renderIndex = runtimeType.RenderCount++;
            int batchIndex = renderIndex / MaxInstancesPerBatch;
            int indexInBatch = renderIndex % MaxInstancesPerBatch;
            float4x4 matrix = frame.Matrices[sourceIndex];
            matrix.c0 *= runtimeType.RenderScale.x;
            matrix.c1 *= runtimeType.RenderScale.y;
            matrix.c2 *= runtimeType.RenderScale.z;
            runtimeType.BatchMatrices[batchIndex][indexInBatch] =
                new Matrix4x4(matrix.c0, matrix.c1, matrix.c2, matrix.c3);
            runtimeType.BatchColors[batchIndex][indexInBatch] = frame.Colors[sourceIndex];
        }

        for (int i = 0; i < _releasedBlockTypes.Count; i++)
        {
            ReleasedBlockRuntimeType runtimeType = _releasedBlockTypes[i];
            runtimeType.BatchCount = Mathf.CeilToInt(runtimeType.RenderCount / (float)MaxInstancesPerBatch);
            for (int batchIndex = 0; batchIndex < runtimeType.BatchCount; batchIndex++)
                runtimeType.BatchCounts[batchIndex] = Mathf.Min(MaxInstancesPerBatch,
                    runtimeType.RenderCount - batchIndex * MaxInstancesPerBatch);
        }
    }
    private void DrawCachedRenderFrame()
    {
        for (int typeIndex = 0; typeIndex < _releasedBlockTypes.Count; typeIndex++)
        {
            ReleasedBlockRuntimeType runtimeType = _releasedBlockTypes[typeIndex];
            for (int batchIndex = 0; batchIndex < runtimeType.BatchCount; batchIndex++)
            {
                _releasedBlockPropertyBlock.Clear();
                _releasedBlockPropertyBlock.SetVectorArray(ColorId, runtimeType.BatchColors[batchIndex]);
                Graphics.DrawMeshInstanced(runtimeType.Mesh, 0, runtimeType.Material,
                    runtimeType.BatchMatrices[batchIndex], runtimeType.BatchCounts[batchIndex],
                    _releasedBlockPropertyBlock, ShadowCastingMode.Off, false, gameObject.layer);
            }
        }
    }
}
