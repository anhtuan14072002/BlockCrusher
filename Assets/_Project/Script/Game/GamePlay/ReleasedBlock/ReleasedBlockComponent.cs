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
    public float SettledTime;
}

[BurstCompile]
[UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
public partial struct ReleasedBlockPlanarConstraintSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new PlanarConstraintJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime
        }.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    private partial struct PlanarConstraintJob : IJobEntity
    {
        public float DeltaTime;

        private void Execute(ref LocalTransform transform, ref PhysicsVelocity velocity,
            ref PhysicsGravityFactor gravity, ref ReleasedBlockComponent block)
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

            float motionSq = math.lengthsq(velocity.Linear.xy);
            bool nearlyStill = motionSq < 0.0025f && math.abs(velocity.Angular.z) < 0.15f;
            if (!nearlyStill)
            {
                block.SettledTime = 0f;
                gravity.Value = 1f;
                return;
            }

            block.SettledTime += DeltaTime;
            if (block.SettledTime < 0.2f)
                return;

            gravity.Value = 0f;
            velocity.Linear = float3.zero;
            velocity.Angular = float3.zero;
        }
    }
}
