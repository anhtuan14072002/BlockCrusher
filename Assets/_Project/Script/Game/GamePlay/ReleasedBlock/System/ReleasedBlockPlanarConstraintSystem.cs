using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
public partial struct ReleasedBlockPlanarConstraintSystem : ISystem
{
    private EntityQuery _query;
    private EntityQuery _suctionQuery;

    public void OnCreate(ref SystemState state)
    {
        _query = SystemAPI.QueryBuilder()
            .WithAllRW<LocalTransform>()
            .WithAllRW<PhysicsVelocity>()
            .WithAll<ReleasedBlockComponent>()
            .Build();
        _suctionQuery = SystemAPI.QueryBuilder()
            .WithAllRW<PhysicsGravityFactor>()
            .WithAll<SuctionTransit>()
            .Build();
        state.RequireForUpdate(_query);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        JobHandle planarHandle = new ReleasedBlockPlanarConstraintJob().ScheduleParallel(_query, state.Dependency);
        state.Dependency = new ReleasedBlockSuctionGravityResetJob().ScheduleParallel(_suctionQuery, planarHandle);
    }
}
