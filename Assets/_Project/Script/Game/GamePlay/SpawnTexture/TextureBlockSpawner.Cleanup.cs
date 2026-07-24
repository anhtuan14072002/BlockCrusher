using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public sealed partial class TextureBlockSpawner
{
    private void TrimReleasedBlockEntityList()
    {
        if (!EnsureEcsReady())
            return;

        for (int i = _releasedBlockEntities.Count - 1; i >= 0; i--)
            if (!_entityManager.Exists(_releasedBlockEntities[i]))
                _releasedBlockEntities.RemoveAt(i);
    }

    private void ClearReleasedBlockEntities()
    {
        if (!EnsureEcsReady())
        {
            _releasedBlockEntities.Clear();
            return;
        }
        for (int i = _releasedBlockEntities.Count - 1; i >= 0; i--)
            DestroyReleasedBlockEntityAt(i);

        using NativeArray<Entity> entities = _releasedBlockQuery.ToEntityArray(Allocator.Temp);
        using NativeArray<ReleasedBlockComponent> blocks =
            _releasedBlockQuery.ToComponentDataArray<ReleasedBlockComponent>(Allocator.Temp);
        for (int i = 0; i < entities.Length; i++)
            if (blocks[i].OwnerId == _ownerId && _entityManager.Exists(entities[i]))
                _entityManager.DestroyEntity(entities[i]);
    }

    private void DestroyReleasedBlockEntityAt(int index)
    {
        if (!EnsureEcsReady())
            return;
        Entity entity = _releasedBlockEntities[index];
        _releasedBlockEntities.RemoveAt(index);
        if (_entityManager.Exists(entity))
            _entityManager.DestroyEntity(entity);
    }

    private void DisposeReleasedBlocks()
    {
        ClearReleasedBlockEntities();
        _releasedBlockEntities.Clear();
        _metaballWaterEntities.Clear();
    }

    private void DisposeReleasedBlockResources()
    {
        DisposeRenderFrameResources();
        if (_releasedBlockCollider.IsCreated)
        {
            _releasedBlockCollider.Dispose();
            _releasedBlockCollider = default;
        }
        if (_releasedBlockMaterial != null)
            DestroyUnityObject(_releasedBlockMaterial);
        _releasedBlockMaterial = null;
        _releasedBlockMesh = null;
    }

    private void EnsureRenderFrameResources()
    {
        if (_renderBatchMatrices == null)
        {
            int batchCapacity = Mathf.CeilToInt(_maxReleasedPhysicsBlocks / (float)MaxInstancesPerBatch);
            _renderBatchMatrices = new Matrix4x4[batchCapacity][];
            _renderBatchColors = new Vector4[batchCapacity][];
            _renderBatchCounts = new int[batchCapacity];
            for (int i = 0; i < batchCapacity; i++)
            {
                _renderBatchMatrices[i] = new Matrix4x4[MaxInstancesPerBatch];
                _renderBatchColors[i] = new Vector4[MaxInstancesPerBatch];
            }
        }

        for (int i = 0; i < _renderFrames.Length; i++)
        {
            if (_renderFrames[i] != null)
                continue;
            _renderFrames[i] = new RenderFrameData(_maxReleasedPhysicsBlocks);
        }
    }

    private void DisposeRenderFrameResources()
    {
        for (int i = 0; i < _renderFrames.Length; i++)
        {
            RenderFrameData frame = _renderFrames[i];
            if (frame == null)
                continue;
            if (frame.Pending)
                frame.Handle.Complete();
            frame.Dispose();
            _renderFrames[i] = null;
        }
        _displayRenderFrame = -1;
        _renderBatchCount = 0;
        _renderBatchMatrices = null;
        _renderBatchColors = null;
        _renderBatchCounts = null;
    }

    private void DisposeReleasedBlockWalls()
    {
        if (_ecsWorld != null && _ecsWorld.IsCreated)
        {
            for (int i = 0; i < _releasedBlockWallEntities.Count; i++)
            {
                Entity entity = _releasedBlockWallEntities[i];
                if (_entityManager.Exists(entity))
                    _entityManager.DestroyEntity(entity);
            }
        }
        _releasedBlockWallEntities.Clear();

        for (int i = 0; i < _releasedBlockWallColliders.Count; i++)
        {
            if (_releasedBlockWallColliders[i].IsCreated)
                _releasedBlockWallColliders[i].Dispose();
        }
        _releasedBlockWallColliders.Clear();
    }

    private void DisposeEcsQuery()
    {
        if (_hasReleasedBlockQuery && _ecsWorld != null && _ecsWorld.IsCreated)
        {
            _releasedBlockQuery.Dispose();
        }
        _releasedBlockQuery = default;
        _hasReleasedBlockQuery = false;
    }
}
