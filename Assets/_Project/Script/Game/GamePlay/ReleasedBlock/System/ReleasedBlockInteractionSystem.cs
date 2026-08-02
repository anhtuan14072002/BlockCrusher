using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

[UpdateInGroup(typeof(BeforePhysicsSystemGroup))]
public partial struct ReleasedBlockInteractionSystem : ISystem
{
    private EntityQuery _query;
    private EntityQuery _suctionTransitQuery;
    private EntityQuery _solidConstraintQuery;
    private NativeList<ReleasedBlockInteractionRequest> _requests;
    private NativeQueue<FixedString64Bytes> _collectedItems;
    private JobHandle _lastInteractionHandle;
    private bool _hasPendingCollection;

    public void OnCreate(ref SystemState state)
    {
        _query = SystemAPI.QueryBuilder()
            .WithAllRW<PhysicsVelocity>()
            .WithAllRW<PhysicsCollider>()
            .WithAllRW<PhysicsGravityFactor>()
            .WithAllRW<ReleasedBlockComponent>()
            .WithAllRW<ReleasedBlockSolidConstraint>()
            .WithAllRW<SuctionTransit>()
            .WithAllRW<Simulate>()
            .WithAllRW<LocalTransform>()
            .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
            .Build();
        _suctionTransitQuery = SystemAPI.QueryBuilder()
            .WithAllRW<PhysicsVelocity>()
            .WithAllRW<PhysicsCollider>()
            .WithAllRW<PhysicsGravityFactor>()
            .WithAllRW<ReleasedBlockComponent>()
            .WithAllRW<SuctionTransit>()
            .WithAllRW<Simulate>()
            .WithAllRW<LocalTransform>()
            .Build();
        _solidConstraintQuery = SystemAPI.QueryBuilder()
            .WithAllRW<PhysicsVelocity>()
            .WithAllRW<ReleasedBlockComponent>()
            .WithAllRW<ReleasedBlockSolidConstraint>()
            .WithAllRW<LocalTransform>()
            .Build();
        _requests = new NativeList<ReleasedBlockInteractionRequest>(8, Allocator.Persistent);
        _collectedItems = new NativeQueue<FixedString64Bytes>(Allocator.Persistent);
    }

    public void OnDestroy(ref SystemState state)
    {
        FlushSuckedCount();
        if (_requests.IsCreated)
            _requests.Dispose();
        if (_collectedItems.IsCreated)
            _collectedItems.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        FlushSuckedCount();

        JobHandle dependency = state.Dependency;
        int requestCount = ReleasedBlockInteractionQueue.Count;
        if (requestCount > 0)
        {
            _requests.ResizeUninitialized(requestCount);
            NativeArray<ReleasedBlockInteractionRequest> requests = _requests.AsArray();
            ReleasedBlockInteractionQueue.CopyToAndClear(requests);
            bool requiresAllBlocks = false;
            for (int i = 0; i < requests.Length; i++)
            {
                ReleasedBlockInteractionRequest request = requests[i];
                if (request.Type != ReleasedBlockInteractionType.Suction || request.AllowSuctionCapture != 0)
                {
                    requiresAllBlocks = true;
                    break;
                }
            }

            EntityCommandBuffer.ParallelWriter commandBuffer = SystemAPI
                .GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged)
                .AsParallelWriter();

            JobHandle interactionHandle = new ReleasedBlockInteractionJob
            {
                Requests = requests,
                CommandBuffer = commandBuffer,
                CollectedItems = _collectedItems.AsParallelWriter()
            }.Schedule(requiresAllBlocks ? _query : _suctionTransitQuery, dependency);

            dependency = interactionHandle;
            _lastInteractionHandle = interactionHandle;
            _hasPendingCollection = true;
        }

        bool scheduledSawPush = false;
        for (int i = LevelMapSpawner.ActiveSpawnerCount - 1; i >= 0; i--)
        {
            LevelMapSpawner spawner = LevelMapSpawner.GetActiveSpawner(i);
            if (spawner == null)
            {
                LevelMapSpawner.RemoveActiveSpawnerAt(i);
                continue;
            }
            if (spawner.TryCreatePendingSawPushJob(out LevelMapSpawner.SawPushJob sawPushJob))
            {
                dependency = sawPushJob.ScheduleParallel(_query, dependency);
                scheduledSawPush = true;
            }
        }

        if (!scheduledSawPush && _solidConstraintQuery.IsEmpty)
        {
            state.Dependency = dependency;
            return;
        }

        for (int i = LevelMapSpawner.ActiveSpawnerCount - 1; i >= 0; i--)
        {
            LevelMapSpawner spawner = LevelMapSpawner.GetActiveSpawner(i);
            if (spawner != null &&
                spawner.TryCreateSolidConstraintJob(
                    out LevelMapSpawner.ReleasedBlockSolidConstraintJob solidConstraintJob))
            {
                dependency = solidConstraintJob.ScheduleParallel(_solidConstraintQuery, dependency);
            }
        }
        state.Dependency = dependency;
    }

    private void FlushSuckedCount()
    {
        if (!_hasPendingCollection)
            return;
        _lastInteractionHandle.Complete();
        _hasPendingCollection = false;
        while (_collectedItems.TryDequeue(out FixedString64Bytes collectibleId))
            LevelMapSpawner.NotifyItemSucked(collectibleId.ToString(), 1);
    }
}
