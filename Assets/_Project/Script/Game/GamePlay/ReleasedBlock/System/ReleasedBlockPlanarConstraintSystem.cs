using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
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
            .WithAllRW<LocalTransform, PhysicsVelocity>()
            .WithAllRW<PhysicsGravityFactor>()
            .WithAllRW<ReleasedBlockComponent>()
            .WithAllRW<Simulate>()
            .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
            .Build();
        state.RequireForUpdate(_query);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new PlanarConstraintJob().ScheduleParallel(_query, state.Dependency);
    }

    [BurstCompile]
    private partial struct PlanarConstraintJob : IJobEntity
    {
        private void Execute(ref LocalTransform transform, ref PhysicsVelocity velocity,
            ref PhysicsGravityFactor gravity, ref ReleasedBlockComponent block, EnabledRefRW<Simulate> simulate)
        {
            transform.Position.z = block.LockedZ;
            velocity.Linear.z = 0f;
            velocity.Angular = float3.zero;
            block.StableFrames = 0;
            gravity.Value = 1f;
            simulate.ValueRW = true;

            float speedSq = math.lengthsq(velocity.Linear.xy);
            float maxSpeedSq = block.MaxPlanarSpeed * block.MaxPlanarSpeed;
            if (speedSq > maxSpeedSq)
            {
                float2 planar = math.normalize(velocity.Linear.xy) * block.MaxPlanarSpeed;
                velocity.Linear.x = planar.x;
                velocity.Linear.y = planar.y;
            }
        }
    }
}
