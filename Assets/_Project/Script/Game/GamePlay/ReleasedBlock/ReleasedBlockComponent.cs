using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using Unity.Burst;

public struct ReleasedBlockComponent : IComponentData
{
    public int OwnerId;
    public float4 Color;
    public float LockedZ;
    public float MaxPlanarSpeed;
}

[BurstCompile]
[UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
public partial struct ReleasedBlockPlanarConstraintSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new PlanarConstraintJob().ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    private partial struct PlanarConstraintJob : IJobEntity
    {
        private void Execute(ref LocalTransform transform, ref PhysicsVelocity velocity,
            in ReleasedBlockComponent block)
        {
            transform.Position.z = block.LockedZ;
            velocity.Linear.z = 0f;

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
