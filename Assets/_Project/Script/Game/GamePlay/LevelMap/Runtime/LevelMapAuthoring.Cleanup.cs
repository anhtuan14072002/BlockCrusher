using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public sealed partial class LevelMapAuthoring
{
    private void CompleteReleasedBlockJobs()
    {
        if (_ecsWorld != null && _ecsWorld.IsCreated)
            _entityManager.CompleteAllTrackedJobs();
    }

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
        _metaballWaterSizes.Clear();
    }

    private void DisposeReleasedBlockResources()
    {
        DisposeRenderFrameResources();
        for (int i = 0; i < _releasedBlockTypes.Count; i++)
        {
            ReleasedBlockRuntimeType runtimeType = _releasedBlockTypes[i];
            if (runtimeType.Collider.IsCreated)
                runtimeType.Collider.Dispose();
            if (runtimeType.Material != null)
                DestroyUnityObject(runtimeType.Material);
        }
        _releasedBlockTypes.Clear();
    }

    private void EnsureRenderFrameResources()
    {
        for (int i = 0; i < _renderFrames.Length; i++)
        {
            if (_renderFrames[i] != null)
                continue;
            _renderFrames[i] = new RenderFrameData(_maxReleasedPhysicsBlocks * 4);
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
