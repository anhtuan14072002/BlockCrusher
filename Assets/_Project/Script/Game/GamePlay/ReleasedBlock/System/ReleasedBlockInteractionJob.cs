using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
internal partial struct ReleasedBlockInteractionJob : IJobEntity
{
    [ReadOnly] public NativeArray<ReleasedBlockInteractionRequest> Requests;
    public EntityCommandBuffer.ParallelWriter CommandBuffer;
    public NativeQueue<FixedString64Bytes>.ParallelWriter CollectedItems;

    private void Execute([EntityIndexInQuery] int sortKey, Entity entity, ref LocalTransform transform,
        ref PhysicsCollider collider, ref PhysicsVelocity velocity, ref PhysicsGravityFactor gravity,
        ref ReleasedBlockComponent block, EnabledRefRW<Simulate> simulate,
        EnabledRefRW<SuctionTransit> suctionTransit)
    {
        float3 position = transform.Position;
        for (int i = 0; i < Requests.Length; i++)
        {
            ReleasedBlockInteractionRequest request = Requests[i];
            switch (request.Type)
            {
                case ReleasedBlockInteractionType.Clear:
                    if (IsInsideBounds(position, request.BoundsMin, request.BoundsMax))
                    {
                        simulate.ValueRW = false;
                        CommandBuffer.DestroyEntity(sortKey, entity);
                        return;
                    }
                    break;

                case ReleasedBlockInteractionType.Conveyor:
                    if (IsInsideBounds(position, request.BoundsMin, request.BoundsMax))
                    {
                        CommandBuffer.SetComponentEnabled<ReleasedBlockSolidConstraint>(sortKey, entity, true);
                        float currentSpeed = math.dot(velocity.Linear, request.Direction);
                        float targetSpeed = MoveTowards(currentSpeed, request.Speed,
                            request.Acceleration * request.DeltaTime);
                        velocity.Linear += request.Direction * (targetSpeed - currentSpeed);
                    }
                    break;

                case ReleasedBlockInteractionType.Suction:
                    bool isFollowingPath = block.SuctionPathIndex != byte.MaxValue;
                    if (!isFollowingPath)
                    {
                        if (request.AllowSuctionCapture == 0)
                            break;

                        float3 local = math.rotate(request.InverseRotation,
                            position - request.Origin - request.BoxOffset);
                        if (math.any(math.abs(local) > request.HalfSize))
                            break;
                    }

                    if (request.SuctionPath.Length == 0)
                        break;

                    int lastPathIndex = request.SuctionPath.Length - 1;
                    int pathIndex = isFollowingPath && block.SuctionPathIndex < request.SuctionPath.Length
                        ? block.SuctionPathIndex
                        : 0;

                    if (!isFollowingPath)
                    {
                        collider.Value = default;
                        block.LockedZ -= request.RenderDepth;
                        CommandBuffer.SetComponentEnabled<ReleasedBlockSolidConstraint>(sortKey, entity, false);
                        suctionTransit.ValueRW = true;
                    }

                    if (pathIndex > 0)
                    {
                        position = GetClosestPointOnSegment(position, request.SuctionPath[pathIndex - 1],
                            request.SuctionPath[pathIndex]);
                        position.z = block.LockedZ;
                        transform.Position = position;
                    }

                    float3 target = pathIndex == 0
                        ? request.SuctionPath[0]
                        : GetSegmentFollowTarget(position, request.SuctionPath[pathIndex - 1],
                            request.SuctionPath[pathIndex], request.PathLookAhead);
                    float3 direction = target - position;
                    direction.z = 0f;
                    float distance = math.length(direction);
                    float3 waypointDelta = request.SuctionPath[pathIndex] - position;
                    waypointDelta.z = 0f;
                    if (pathIndex < lastPathIndex && math.lengthsq(waypointDelta) <=
                        request.WaypointRadius * request.WaypointRadius)
                    {
                        pathIndex++;
                        block.SuctionPathIndex = (byte)pathIndex;
                        target = GetSegmentFollowTarget(position, request.SuctionPath[pathIndex - 1],
                            request.SuctionPath[pathIndex], request.PathLookAhead);
                        direction = target - position;
                        direction.z = 0f;
                        distance = math.length(direction);
                    }

                    float3 suctionDelta = request.SuctionPath[lastPathIndex] - position;
                    suctionDelta.z = 0f;
                    if (pathIndex == lastPathIndex &&
                        math.lengthsq(suctionDelta) < request.DestroyRadius * request.DestroyRadius)
                    {
                        if (!block.CollectibleId.IsEmpty)
                            CollectedItems.Enqueue(block.CollectibleId);
                        simulate.ValueRW = false;
                        CommandBuffer.DestroyEntity(sortKey, entity);
                        return;
                    }

                    if (distance <= 0.0001f)
                        break;

                    direction /= distance;
                    block.SuctionPathIndex = (byte)pathIndex;
                    simulate.ValueRW = true;
                    gravity.Value = 0f;
                    float3 targetVelocity = direction * math.min(request.Force * distance, request.MaxVelocity);
                    float3 movedVelocity = MoveTowards(velocity.Linear, targetVelocity,
                        request.Acceleration * request.DeltaTime);
                    if (distance <= request.DestroyRadius * 2.5f)
                        movedVelocity = math.lerp(movedVelocity, targetVelocity,
                            math.saturate(request.ArrivalDamping * request.DeltaTime));
                    velocity.Linear = ClampMagnitude(movedVelocity, request.MaxVelocity);
                    break;
            }
        }

        if (block.SuctionPathIndex == byte.MaxValue)
            velocity.Linear = ClampMagnitude(velocity.Linear, block.MaxPlanarSpeed);
    }

    private static float3 GetSegmentFollowTarget(float3 position, float3 segmentStart, float3 segmentEnd,
        float lookAhead)
    {
        position.z = 0f;
        segmentStart.z = 0f;
        segmentEnd.z = 0f;
        float3 segment = segmentEnd - segmentStart;
        float segmentLengthSq = math.lengthsq(segment);
        if (segmentLengthSq <= 0.000001f)
            return segmentEnd;

        float progress = math.saturate(math.dot(position - segmentStart, segment) / segmentLengthSq);
        float segmentLength = math.sqrt(segmentLengthSq);
        float targetProgress = math.min(1f, progress + lookAhead / segmentLength);
        return math.lerp(segmentStart, segmentEnd, targetProgress);
    }

    private static float3 GetClosestPointOnSegment(float3 position, float3 segmentStart, float3 segmentEnd)
    {
        float3 segment = segmentEnd - segmentStart;
        segment.z = 0f;
        float segmentLengthSq = math.lengthsq(segment);
        if (segmentLengthSq <= 0.000001f)
            return segmentEnd;

        float3 fromStart = position - segmentStart;
        fromStart.z = 0f;
        float progress = math.saturate(math.dot(fromStart, segment) / segmentLengthSq);
        return math.lerp(segmentStart, segmentEnd, progress);
    }

    private static bool IsInsideBounds(float3 position, float3 min, float3 max)
    {
        return math.all(position >= min & position <= max);
    }

    private static float MoveTowards(float current, float target, float maxDelta)
    {
        float delta = target - current;
        return math.abs(delta) <= maxDelta ? target : current + math.sign(delta) * maxDelta;
    }

    private static float3 MoveTowards(float3 current, float3 target, float maxDelta)
    {
        float3 delta = target - current;
        float distance = math.length(delta);
        return distance <= maxDelta || distance <= 0f ? target : current + delta / distance * maxDelta;
    }

    private static float3 ClampMagnitude(float3 value, float maxLength)
    {
        float lengthSq = math.lengthsq(value);
        float maxLengthSq = maxLength * maxLength;
        return lengthSq > maxLengthSq ? value * (maxLength * math.rsqrt(lengthSq)) : value;
    }
}
