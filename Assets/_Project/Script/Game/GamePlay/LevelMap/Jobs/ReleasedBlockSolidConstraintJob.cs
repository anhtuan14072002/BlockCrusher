using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

public sealed partial class LevelMapSpawner
{
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    internal partial struct ReleasedBlockSolidConstraintJob : IJobEntity
    {
        [ReadOnly] public NativeArray<byte> CellSolid;
        public float4x4 WorldToLocal;
        public float4x4 LocalToWorld;
        public float3 Offset;
        public float CellSize;
        public float2 GridBoundsMin;
        public float2 GridBoundsMax;
        public float DeltaTime;
        public int GridWidth;
        public int GridHeight;
        public int OwnerId;

        private void Execute(ref LocalTransform transform, ref PhysicsVelocity velocity,
            ref ReleasedBlockComponent block, EnabledRefRW<ReleasedBlockSolidConstraint> solidConstraint)
        {
            if (block.OwnerId != OwnerId)
                return;
            if (block.SuctionPathIndex != byte.MaxValue || block.SolidConstraintFrames == 0)
            {
                block.SolidConstraintFrames = 0;
                solidConstraint.ValueRW = false;
                return;
            }

            block.SolidConstraintFrames--;

            float3 position = transform.Position;
            float3 resolvedPosition = ResolvePenetration(position, block.Radius);
            ApplyCorrection(ref velocity, resolvedPosition - position);
            transform.Position = resolvedPosition;

            float3 predictedPosition = resolvedPosition + velocity.Linear * DeltaTime;
            predictedPosition.z = resolvedPosition.z;
            float3 resolvedPrediction = ResolvePenetration(predictedPosition, block.Radius);
            ApplyCorrection(ref velocity, resolvedPrediction - predictedPosition);

            if (block.SolidConstraintFrames == 0)
                solidConstraint.ValueRW = false;
        }

        private float3 ResolvePenetration(float3 worldPosition, float blockRadius)
        {
            float3 localPosition = math.transform(WorldToLocal, worldPosition);
            float2 position = localPosition.xy;
            if (math.any((position < GridBoundsMin) | (position > GridBoundsMax)))
                return worldPosition;

            for (int iteration = 0; iteration < 4; iteration++)
            {
                float2 previousPosition = position;
                int centerX = (int)math.round((position.x - Offset.x) / CellSize);
                int centerY = (int)math.round((position.y - Offset.y) / CellSize);

                for (int y = centerY - 1; y <= centerY + 1; y++)
                {
                    if ((uint)y >= (uint)GridHeight) continue;

                    for (int x = centerX - 1; x <= centerX + 1; x++)
                    {
                        if ((uint)x >= (uint)GridWidth || CellSolid[y * GridWidth + x] == 0) continue;
                        position = ResolveCellPenetration(position, x, y, blockRadius);
                    }
                }

                if (math.lengthsq(position - previousPosition) <= 0.00000001f) break;
            }

            float3 resolvedWorldPosition = math.transform(LocalToWorld,
                new float3(position.x, position.y, localPosition.z));
            resolvedWorldPosition.z = worldPosition.z;
            return resolvedWorldPosition;
        }

        private float2 ResolveCellPenetration(float2 position, int cellX, int cellY, float blockRadius)
        {
            float2 cellCenter = Offset.xy + new float2(cellX, cellY) * CellSize;
            float halfCell = CellSize * 0.5f;
            float2 boundsMin = cellCenter - halfCell;
            float2 boundsMax = cellCenter + halfCell;
            float2 closest = math.clamp(position, boundsMin, boundsMax);
            float2 delta = position - closest;
            float distanceSq = math.lengthsq(delta);
            float radiusSq = blockRadius * blockRadius;
            if (distanceSq >= radiusSq)
                return position;

            if (distanceSq > 0.00000001f)
                return position + delta * (blockRadius * math.rsqrt(distanceSq) - 1f);

            float left = position.x - boundsMin.x;
            float right = boundsMax.x - position.x;
            float down = position.y - boundsMin.y;
            float up = boundsMax.y - position.y;
            float nearest = math.min(math.min(left, right), math.min(down, up));
            if (nearest == left)
                position.x = boundsMin.x - blockRadius;
            else if (nearest == right)
                position.x = boundsMax.x + blockRadius;
            else if (nearest == down)
                position.y = boundsMin.y - blockRadius;
            else
                position.y = boundsMax.y + blockRadius;
            return position;
        }

        private static void ApplyCorrection(ref PhysicsVelocity velocity, float3 correction)
        {
            correction.z = 0f;
            float correctionLengthSq = math.lengthsq(correction);
            if (correctionLengthSq <= 0.00000001f)
                return;

            float3 normal = correction * math.rsqrt(correctionLengthSq);
            float inwardSpeed = math.dot(velocity.Linear, normal);
            if (inwardSpeed < 0f)
                velocity.Linear -= normal * inwardSpeed;
        }
    }
}
