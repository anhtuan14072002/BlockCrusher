using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
public partial struct ReleasedBlockPlanarConstraintSystem : ISystem
{
    private EntityQuery _query;

    public void OnCreate(ref SystemState state)
    {
        _query = SystemAPI.QueryBuilder()
            .WithAllRW<LocalTransform>()
            .WithAllRW<PhysicsVelocity>()
            .WithAllRW<PhysicsGravityFactor>()
            .WithAll<ReleasedBlockComponent, Simulate>()
            .Build();
        state.RequireForUpdate(_query);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new ReleasedBlockPlanarConstraintJob().ScheduleParallel(_query, state.Dependency);
    }
}
