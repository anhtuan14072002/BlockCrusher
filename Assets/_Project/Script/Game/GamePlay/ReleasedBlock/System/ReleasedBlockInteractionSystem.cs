using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

[UpdateInGroup(typeof(BeforePhysicsSystemGroup))]
public partial struct ReleasedBlockPrePhysicsConstraintSystem : ISystem
{
    private EntityQuery _query;

    public void OnCreate(ref SystemState state)
    {
        _query = SystemAPI.QueryBuilder()
            .WithAllRW<PhysicsVelocity>()
            .WithAllRW<ReleasedBlockComponent>()
            .WithAllRW<ReleasedBlockSolidConstraint>()
            .WithAllRW<LocalTransform>()
            .Build();
    }

    public void OnUpdate(ref SystemState state)
    {
        JobHandle dependency = state.Dependency;
        for (int i = LevelMapAuthoring.ActiveSpawnerCount - 1; i >= 0; i--)
        {
            LevelMapAuthoring spawner = LevelMapAuthoring.GetActiveSpawner(i);
            if (spawner != null &&
                spawner.TryCreateSolidConstraintJob(false,
                    out LevelMapAuthoring.ReleasedBlockSolidConstraintJob solidConstraintJob))
            {
                dependency = solidConstraintJob.ScheduleParallel(_query, dependency);
            }
        }

        state.Dependency = dependency;
    }
}
