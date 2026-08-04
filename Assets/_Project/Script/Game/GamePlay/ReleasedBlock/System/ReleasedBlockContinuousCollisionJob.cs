using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
internal partial struct ReleasedBlockContinuousCollisionJob : IJobEntity
{
    [ReadOnly] public CollisionWorld CollisionWorld;

    private void Execute(Entity entity, ref LocalTransform transform, ref PhysicsVelocity velocity,
        in PhysicsCollider collider, ref ReleasedBlockComponent block)
    {
        if (!collider.IsValid || block.SuctionPathIndex != byte.MaxValue)
            return;

        float3 start = block.PhysicsStepStartPosition;
        float3 end = transform.Position;
        start.z = block.LockedZ;
        end.z = block.LockedZ;
        float3 travel = end - start;
        float travelLength = math.length(travel.xy);
        if (travelLength <= 0.0001f)
        {
            block.PhysicsStepStartPosition = end;
            return;
        }

        ColliderCastInput input = new ColliderCastInput(
            collider.Value, start, end, transform.Rotation, transform.Scale);
        ClosestOtherHitCollector collector = new ClosestOtherHitCollector(entity);
        if (!CollisionWorld.CastCollider(input, ref collector) || collector.NumHits == 0)
        {
            block.PhysicsStepStartPosition = end;
            return;
        }

        ColliderCastHit hit = collector.ClosestHit;
        float skinFraction = math.min(0.01f / travelLength, 0.05f);
        float safeFraction = math.max(0f, hit.Fraction - skinFraction);
        transform.Position = math.lerp(start, end, safeFraction);
        transform.Position.z = block.LockedZ;

        float3 normal = math.normalizesafe(new float3(hit.SurfaceNormal.xy, 0f));
        float inwardSpeed = math.dot(velocity.Linear, normal);
        if (inwardSpeed < 0f)
            velocity.Linear -= normal * inwardSpeed;
        block.PhysicsStepStartPosition = transform.Position;
    }

    private struct ClosestOtherHitCollector : ICollector<ColliderCastHit>
    {
        public bool EarlyOutOnFirstHit => false;
        public float MaxFraction => 1f;
        public int NumHits { get; private set; }
        public ColliderCastHit ClosestHit { get; private set; }

        private Entity _ignoredEntity;
        private float _closestFraction;

        public ClosestOtherHitCollector(Entity ignoredEntity)
        {
            _ignoredEntity = ignoredEntity;
            _closestFraction = float.MaxValue;
            NumHits = 0;
            ClosestHit = default;
        }

        public bool AddHit(ColliderCastHit hit)
        {
            if (hit.Entity == _ignoredEntity || hit.Fraction <= 0.0001f)
                return false;

            if (hit.Fraction < _closestFraction)
            {
                _closestFraction = hit.Fraction;
                ClosestHit = hit;
            }
            NumHits++;
            return true;
        }
    }
}
