using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

public sealed partial class TextureBlockSpawner
{
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    internal partial struct SawPushJob : IJobEntity
    {
        [ReadOnly] public NativeArray<byte> CellSolid;
        public float4x4 WorldToLocal;
        public float3 Offset;
        public float3 SawCenter;
        public float3 PressDirection;
        public float PressSpeed;
        public float OutwardForce;
        public float TangentialForce;
        public float SpinDirection;
        public float BladeRadius;
        public float MaxVelocity;
        public float CellSize;
        public int GridWidth;
        public int GridHeight;
        public int OwnerId;

        private void Execute(in LocalTransform transform, ref ReleasedBlockComponent block,
            ref PhysicsVelocity velocity, EnabledRefRW<ReleasedBlockSolidConstraint> solidConstraint,
            EnabledRefRO<SuctionTransit> suctionTransit)
        {
            if (block.OwnerId != OwnerId || suctionTransit.ValueRO)
                return;

            float3 position = transform.Position;
            float2 delta = position.xy - SawCenter.xy;
            float distanceSq = math.lengthsq(delta);
            float radiusSq = BladeRadius * BladeRadius;
            if (distanceSq > radiusSq)
                return;

            block.SolidConstraintFrames = ReleasedBlockComponent.SolidConstraintDuration;
            solidConstraint.ValueRW = true;
            float distance = math.sqrt(distanceSq);
            float2 outward = distance > 0.0001f
                ? delta / distance
                : math.normalizesafe(PressDirection.xy, new float2(0f, 1f));
            float radiusPush = 1f - math.saturate(distance / BladeRadius);
            float spinSign = SpinDirection >= 0f ? 1f : -1f;
            float2 tangent = new float2(-outward.y, outward.x) * spinSign;
            float speedScale = 1f + math.min(PressSpeed, 4f) * 0.1f;
            float2 impulse = outward * (OutwardForce * 0.08f * speedScale * radiusPush) +
                             tangent * (TangentialForce * 0.22f * speedScale * radiusPush);
            float3 linear = new float3(velocity.Linear.xy + impulse, 0f);
            linear = ClampMagnitude(linear, MaxVelocity);
            velocity.Linear = RedirectVelocityFromSolid(position, linear);
        }

        private float3 RedirectVelocityFromSolid(float3 worldPosition, float3 velocity)
        {
            velocity.z = 0f;
            float speed = math.length(velocity);
            if (speed <= 0.0001f)
                return velocity;
            float3 direction = velocity / speed;
            float nearProbeDistance = CellSize * 0.9f;
            float farProbeDistance = CellSize * 1.6f;
            if (!IsSolidAlongDirection(worldPosition, direction, nearProbeDistance, farProbeDistance))
                return velocity;

            float3 tangent = new float3(-direction.y, direction.x, 0f);
            float3 bestDirection = float3.zero;
            float bestAlignment = float.MinValue;
            EvaluateFreeDirection(worldPosition, tangent, direction, nearProbeDistance, farProbeDistance,
                ref bestDirection, ref bestAlignment);
            EvaluateFreeDirection(worldPosition, -tangent, direction, nearProbeDistance, farProbeDistance,
                ref bestDirection, ref bestAlignment);
            EvaluateFreeDirection(worldPosition, new float3(0f, 1f, 0f), direction, nearProbeDistance,
                farProbeDistance, ref bestDirection, ref bestAlignment);
            EvaluateFreeDirection(worldPosition, new float3(0f, -1f, 0f), direction, nearProbeDistance,
                farProbeDistance, ref bestDirection, ref bestAlignment);
            EvaluateFreeDirection(worldPosition, new float3(-1f, 0f, 0f), direction, nearProbeDistance,
                farProbeDistance, ref bestDirection, ref bestAlignment);
            EvaluateFreeDirection(worldPosition, new float3(1f, 0f, 0f), direction, nearProbeDistance,
                farProbeDistance, ref bestDirection, ref bestAlignment);
            return math.lengthsq(bestDirection) > 0f ? bestDirection * speed : float3.zero;
        }

        private void EvaluateFreeDirection(float3 worldPosition, float3 candidate, float3 desired,
            float nearProbeDistance, float farProbeDistance, ref float3 bestDirection, ref float bestAlignment)
        {
            candidate = math.normalizesafe(new float3(candidate.xy, 0f));
            if (IsSolidAlongDirection(worldPosition, candidate, nearProbeDistance, farProbeDistance))
                return;
            float alignment = math.dot(candidate, desired);
            if (alignment <= bestAlignment)
                return;
            bestAlignment = alignment;
            bestDirection = candidate;
        }

        private bool IsSolidAlongDirection(float3 worldPosition, float3 direction,
            float nearProbeDistance, float farProbeDistance)
        {
            float3 lateralOffset = new float3(-direction.y, direction.x, 0f) * (CellSize * 0.45f);
            float3 nearPoint = worldPosition + direction * nearProbeDistance;
            float3 farPoint = worldPosition + direction * farProbeDistance;
            return IsSolidAtWorldCell(nearPoint) ||
                   IsSolidAtWorldCell(farPoint) ||
                   IsSolidAtWorldCell(nearPoint + lateralOffset) ||
                   IsSolidAtWorldCell(nearPoint - lateralOffset) ||
                   IsSolidAtWorldCell(farPoint + lateralOffset) ||
                   IsSolidAtWorldCell(farPoint - lateralOffset);
        }

        private bool IsSolidAtWorldCell(float3 worldPosition)
        {
            float3 local = math.transform(WorldToLocal, worldPosition);
            int x = (int)math.round((local.x - Offset.x) / CellSize);
            int y = (int)math.round((local.y - Offset.y) / CellSize);
            if ((uint)x >= (uint)GridWidth || (uint)y >= (uint)GridHeight)
                return false;
            return CellSolid[y * GridWidth + x] != 0;
        }

        private static float3 ClampMagnitude(float3 value, float maxLength)
        {
            float lengthSq = math.lengthsq(value);
            float maxLengthSq = maxLength * maxLength;
            return lengthSq > maxLengthSq ? value * (maxLength * math.rsqrt(lengthSq)) : value;
        }
    }
}
