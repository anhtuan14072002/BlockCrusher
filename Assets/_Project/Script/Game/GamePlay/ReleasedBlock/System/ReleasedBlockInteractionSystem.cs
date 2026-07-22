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
    private EntityQuery _motionQuery;
    private EntityQuery _suctionTransitQuery;
    private NativeReference<int> _suckedCount;
    private JobHandle _lastInteractionHandle;
    private bool _hasPendingSuckedCount;

    public void OnCreate(ref SystemState state)
    {
        _query = SystemAPI.QueryBuilder()
            .WithAllRW<PhysicsVelocity>()
            .WithAllRW<PhysicsCollider>()
            .WithAllRW<PhysicsGravityFactor>()
            .WithAllRW<ReleasedBlockComponent>()
            .WithAllRW<SuctionTransit>()
            .WithAllRW<Simulate>()
            .WithAllRW<LocalTransform>()
            .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
            .Build();
        _motionQuery = SystemAPI.QueryBuilder()
            .WithAllRW<LocalTransform>()
            .WithAllRW<PhysicsVelocity>()
            .WithAll<ReleasedBlockComponent>()
            .WithAll<SuctionTransit>()
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
        _suckedCount = new NativeReference<int>(Allocator.Persistent);
    }

    public void OnDestroy(ref SystemState state)
    {
        FlushSuckedCount();
        if (_suckedCount.IsCreated)
            _suckedCount.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        FlushSuckedCount();

        JobHandle dependency = state.Dependency;
        int requestCount = ReleasedBlockInteractionQueue.Count;
        if (requestCount > 0)
        {
            NativeArray<ReleasedBlockInteractionRequest> requests =
                new NativeArray<ReleasedBlockInteractionRequest>(requestCount, Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory);
            ReleasedBlockInteractionQueue.CopyToAndClear(requests);
            _suckedCount.Value = 0;
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
                SuckedCount = _suckedCount
            }.Schedule(requiresAllBlocks ? _query : _suctionTransitQuery, dependency);

            dependency = JobHandle.CombineDependencies(interactionHandle, requests.Dispose(interactionHandle));
            _lastInteractionHandle = interactionHandle;
            _hasPendingSuckedCount = true;
        }

        for (int i = TextureBlockSpawner.ActiveSpawnerCount - 1; i >= 0; i--)
        {
            TextureBlockSpawner spawner = TextureBlockSpawner.GetActiveSpawner(i);
            if (spawner == null)
            {
                TextureBlockSpawner.RemoveActiveSpawnerAt(i);
                continue;
            }
            if (spawner.TryCreatePendingSawPushJob(out TextureBlockSpawner.SawPushJob sawPushJob))
                dependency = sawPushJob.ScheduleParallel(_motionQuery, dependency);
        }

        for (int i = TextureBlockSpawner.ActiveSpawnerCount - 1; i >= 0; i--)
        {
            TextureBlockSpawner spawner = TextureBlockSpawner.GetActiveSpawner(i);
            if (spawner != null &&
                spawner.TryCreateSolidConstraintJob(
                    out TextureBlockSpawner.ReleasedBlockSolidConstraintJob solidConstraintJob))
            {
                dependency = solidConstraintJob.ScheduleParallel(_motionQuery, dependency);
            }
        }
        state.Dependency = dependency;
    }

    private void FlushSuckedCount()
    {
        if (!_hasPendingSuckedCount)
            return;
        _lastInteractionHandle.Complete();
        _hasPendingSuckedCount = false;
        int count = _suckedCount.Value;
        if (count > 0)
            TextureBlockSpawner.NotifyBlocksSucked(count);
    }
}
