using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

public sealed partial class LevelMapAuthoring
{
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    internal partial struct CutDebrisMapGuardJob : IJobEntity
    {
        [ReadOnly] public NativeArray<byte> CellSolid;
        public float4x4 WorldToLocal;
        public float3 Offset;
        public float2 CellSize;
        public int GridWidth;
        public int GridHeight;
        public int OwnerId;

        private void Execute(ref LocalTransform transform, ref PhysicsVelocity velocity,
            ref Crusher.CutDebrisComponent debris)
        {
            Apply(ref transform, ref velocity, ref debris);
        }

        internal void Apply(ref LocalTransform transform, ref PhysicsVelocity velocity,
            ref Crusher.CutDebrisComponent debris)
        {
            if (debris.OwnerId != OwnerId)
                return;

            float3 position = transform.Position;
            if (!IsInsideSolidCell(position))
            {
                debris.PhysicsStepStartPosition = position;
                return;
            }

            float3 safePosition = debris.PhysicsStepStartPosition;
            if (IsInsideSolidCell(safePosition))
                return;

            float3 correction = safePosition - position;
            correction.z = 0f;
            float correctionLengthSq = math.lengthsq(correction);
            if (correctionLengthSq > 0.00000001f)
            {
                float3 normal = correction * math.rsqrt(correctionLengthSq);
                float inwardSpeed = math.dot(velocity.Linear, normal);
                if (inwardSpeed < 0f)
                    velocity.Linear -= normal * inwardSpeed;
            }

            safePosition.z = position.z;
            transform.Position = safePosition;
            velocity.Angular.z = 0f;
        }

        private bool IsInsideSolidCell(float3 worldPosition)
        {
            float3 localPosition = math.transform(WorldToLocal, worldPosition);
            int x = (int)math.round((localPosition.x - Offset.x) / CellSize.x);
            int y = (int)math.round((localPosition.y - Offset.y) / CellSize.y);
            return (uint)x < (uint)GridWidth && (uint)y < (uint)GridHeight &&
                   CellSolid[y * GridWidth + x] != 0;
        }
    }

#if UNITY_EDITOR
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ValidateCutDebrisMapGuard()
    {
        NativeArray<byte> solid = new(2, Allocator.Temp);
        solid[1] = 1;
        CutDebrisMapGuardJob job = new()
        {
            CellSolid = solid,
            WorldToLocal = float4x4.identity,
            Offset = float3.zero,
            CellSize = new float2(1f),
            GridWidth = 2,
            GridHeight = 1,
            OwnerId = 7
        };
        LocalTransform transform = LocalTransform.FromPosition(new float3(1f, 0f, 0f));
        PhysicsVelocity velocity = new()
        {
            Linear = new float3(1f, -2f, 0f),
            Angular = new float3(0f, 0f, 1f)
        };
        Crusher.CutDebrisComponent debris = new()
        {
            OwnerId = 7,
            PhysicsStepStartPosition = float3.zero
        };

        job.Apply(ref transform, ref velocity, ref debris);
        UnityEngine.Debug.Assert(math.lengthsq(transform.Position) < 0.000001f &&
                                 math.abs(velocity.Linear.x) < 0.0001f &&
                                 math.abs(velocity.Linear.y + 2f) < 0.0001f &&
                                 math.abs(velocity.Angular.z) < 0.0001f,
            "Cut debris map guard must restore the last valid position without cancelling gravity.");
        solid.Dispose();
    }
#endif

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    internal partial struct ReleasedBlockSolidConstraintJob : IJobEntity
    {
        [ReadOnly] public NativeArray<byte> CellSolid;
        public float4x4 WorldToLocal;
        public float4x4 LocalToWorld;
        public float3 Offset;
        public float2 CellSize;
        public float2 GridBoundsMin;
        public float2 GridBoundsMax;
        public float DeltaTime;
        public float LocalRadiusScale;
        public byte ResolveAfterPhysics;
        public int GridWidth;
        public int GridHeight;
        public int OwnerId;

        private void Execute(ref LocalTransform transform, ref PhysicsVelocity velocity,
            ref ReleasedBlockComponent block, EnabledRefRW<ReleasedBlockSolidConstraint> solidConstraint)
        {
            if (block.OwnerId != OwnerId)
                return;
            if (block.SuctionPathIndex != byte.MaxValue)
            {
                solidConstraint.ValueRW = false;
                return;
            }

            float3 position = transform.Position;
            float localRadius = block.Radius * LocalRadiusScale;

            if (ResolveAfterPhysics != 0)
            {
                transform.Position = ResolveSweptMotion(
                    block.PhysicsStepStartPosition, position, localRadius, ref velocity);
                block.PhysicsStepStartPosition = transform.Position;
                DisableOutsideGrid(transform.Position, localRadius, ref solidConstraint);
                return;
            }

            float3 localPosition = math.transform(WorldToLocal, position);
            float2 constraintMargin = new float2(localRadius) + CellSize;
            if (math.any((localPosition.xy < GridBoundsMin - constraintMargin) |
                         (localPosition.xy > GridBoundsMax + constraintMargin)))
            {
                solidConstraint.ValueRW = false;
                return;
            }

            float3 resolvedPosition = ResolvePenetration(position, localRadius);
            ApplyCorrection(ref velocity, resolvedPosition - position);
            transform.Position = resolvedPosition;
            block.PhysicsStepStartPosition = resolvedPosition;

            float3 predictedPosition = resolvedPosition + velocity.Linear * DeltaTime;
            predictedPosition.z = resolvedPosition.z;
            float3 resolvedPrediction = ResolvePenetration(predictedPosition, localRadius);
            ApplyCorrection(ref velocity, resolvedPrediction - predictedPosition);
        }

        private float3 ResolveSweptMotion(float3 startWorld, float3 endWorld, float localBlockRadius,
            ref PhysicsVelocity velocity)
        {
            float3 resolvedStart = ResolvePenetration(startWorld, localBlockRadius);
            ApplyCorrection(ref velocity, resolvedStart - startWorld);
            startWorld = resolvedStart;

            float3 startLocal = math.transform(WorldToLocal, startWorld);
            float3 endLocal = math.transform(WorldToLocal, endWorld);
            float localDistance = math.distance(startLocal.xy, endLocal.xy);
            int requiredSteps = (int)math.ceil(
                localDistance / math.max(math.cmin(CellSize) * 0.2f, 0.0001f));
            if (requiredSteps <= 0)
                return ResolvePenetration(endWorld, localBlockRadius);

            if (requiredSteps > 128)
            {
                velocity.Linear.xy = float2.zero;
                return startWorld;
            }

            for (int step = 1; step <= requiredSteps; step++)
            {
                float3 sampleWorld = math.lerp(startWorld, endWorld, step / (float)requiredSteps);
                float3 resolvedSample = ResolvePenetration(sampleWorld, localBlockRadius);
                float3 correction = resolvedSample - sampleWorld;
                if (math.lengthsq(correction.xy) <= 0.00000001f)
                    continue;

                ApplyCorrection(ref velocity, correction);
                return resolvedSample;
            }

            return endWorld;
        }

        private void DisableOutsideGrid(float3 worldPosition, float localBlockRadius,
            ref EnabledRefRW<ReleasedBlockSolidConstraint> solidConstraint)
        {
            float2 localPosition = math.transform(WorldToLocal, worldPosition).xy;
            float2 margin = new float2(localBlockRadius) + CellSize;
            if (math.any((localPosition < GridBoundsMin - margin) | (localPosition > GridBoundsMax + margin)))
                solidConstraint.ValueRW = false;
        }

        private float3 ResolvePenetration(float3 worldPosition, float localBlockRadius)
        {
            float3 localPosition = math.transform(WorldToLocal, worldPosition);
            float2 position = localPosition.xy;
            if (math.any((position < GridBoundsMin) | (position > GridBoundsMax)))
                return worldPosition;

            for (int iteration = 0; iteration < 4; iteration++)
            {
                float2 previousPosition = position;
                int centerX = (int)math.round((position.x - Offset.x) / CellSize.x);
                int centerY = (int)math.round((position.y - Offset.y) / CellSize.y);

                int searchRadiusX = (int)math.ceil(localBlockRadius / CellSize.x + 0.5f);
                int searchRadiusY = (int)math.ceil(localBlockRadius / CellSize.y + 0.5f);
                for (int y = centerY - searchRadiusY; y <= centerY + searchRadiusY; y++)
                {
                    if ((uint)y >= (uint)GridHeight) continue;

                    for (int x = centerX - searchRadiusX; x <= centerX + searchRadiusX; x++)
                    {
                        if ((uint)x >= (uint)GridWidth || CellSolid[y * GridWidth + x] == 0) continue;
                        position = ResolveCellPenetration(position, x, y, localBlockRadius);
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
            float2 halfCell = CellSize * 0.5f;
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
