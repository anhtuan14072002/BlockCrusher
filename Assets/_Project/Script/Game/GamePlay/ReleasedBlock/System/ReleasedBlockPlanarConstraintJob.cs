using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[BurstCompile]
internal partial struct ReleasedBlockPlanarConstraintJob : IJobEntity
{
    private void Execute(ref LocalTransform transform, ref PhysicsVelocity velocity,
        ref PhysicsGravityFactor gravity, ref ReleasedBlockComponent block, EnabledRefRW<Simulate> simulate)
    {
        transform.Position.z = block.LockedZ;
        velocity.Linear.z = 0f;
        velocity.Angular.x = 0f;
        velocity.Angular.y = 0f;
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
