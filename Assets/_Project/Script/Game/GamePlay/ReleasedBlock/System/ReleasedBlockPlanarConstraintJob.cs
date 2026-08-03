using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[BurstCompile]
internal partial struct ReleasedBlockPlanarConstraintJob : IJobEntity
{
    private void Execute(ref LocalTransform transform, ref PhysicsVelocity velocity,
        ref PhysicsGravityFactor gravity, in ReleasedBlockComponent block)
    {
        transform.Position.z = block.LockedZ;
        velocity.Linear.z = 0f;
        velocity.Angular.x = 0f;
        velocity.Angular.y = 0f;
        gravity.Value = block.RenderAsMetaball != 0 ? 0f : 1f;

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
