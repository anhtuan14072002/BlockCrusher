#pragma warning disable SGICE003
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

[UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
[UpdateBefore(typeof(ReleasedBlockPlanarConstraintSystem))]
public partial struct ReleasedBlockPostPhysicsConstraintSystem : ISystem
{
    private EntityQuery _query;
    private EntityQuery _continuousCollisionQuery;
    private EntityQuery _unconstrainedContinuousCollisionQuery;

    public void OnCreate(ref SystemState state)
    {
        _query = SystemAPI.QueryBuilder()
            .WithAllRW<LocalTransform>()
            .WithAllRW<PhysicsVelocity>()
            .WithAllRW<ReleasedBlockComponent>()
            .WithAllRW<ReleasedBlockSolidConstraint>()
            .Build();
        _continuousCollisionQuery = SystemAPI.QueryBuilder()
            .WithAllRW<LocalTransform>()
            .WithAllRW<PhysicsVelocity>()
            .WithAll<PhysicsCollider>()
            .WithAll<ReleasedBlockComponent, ReleasedBlockSolidConstraint, Simulate>()
            .WithDisabled<SuctionTransit>()
            .Build();
        _unconstrainedContinuousCollisionQuery = SystemAPI.QueryBuilder()
            .WithAllRW<LocalTransform>()
            .WithAllRW<PhysicsVelocity>()
            .WithAll<PhysicsCollider, ReleasedBlockComponent, Simulate>()
            .WithDisabled<ReleasedBlockSolidConstraint, SuctionTransit>()
            .Build();
        state.RequireForUpdate<PhysicsWorldSingleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        JobHandle dependency = new ReleasedBlockContinuousCollisionJob
        {
            CollisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld,
            CommitStepStartPosition = 0
        }.ScheduleParallel(_continuousCollisionQuery, state.Dependency);
        dependency = new ReleasedBlockContinuousCollisionJob
        {
            CollisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld,
            CommitStepStartPosition = 1
        }.ScheduleParallel(_unconstrainedContinuousCollisionQuery, dependency);
        for (int i = LevelMapAuthoring.ActiveSpawnerCount - 1; i >= 0; i--)
        {
            LevelMapAuthoring spawner = LevelMapAuthoring.GetActiveSpawner(i);
            if (spawner == null)
            {
                LevelMapAuthoring.RemoveActiveSpawnerAt(i);
                continue;
            }

            if (spawner.TryCreateSolidConstraintJob(true,
                    out LevelMapAuthoring.ReleasedBlockSolidConstraintJob solidConstraintJob))
            {
                dependency = solidConstraintJob.ScheduleParallel(_query, dependency);
            }
        }

        state.Dependency = dependency;
    }
}

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
#pragma warning restore SGICE003
