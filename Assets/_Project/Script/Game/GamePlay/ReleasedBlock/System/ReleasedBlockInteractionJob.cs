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
    public NativeReference<int> SuckedCount;

    private void Execute([EntityIndexInQuery] int sortKey, Entity entity, in LocalTransform transform,
        ref PhysicsVelocity velocity, ref PhysicsGravityFactor gravity, ref ReleasedBlockComponent block,
        EnabledRefRW<Simulate> simulate)
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
                        float currentSpeed = math.dot(velocity.Linear, request.Direction);
                        float targetSpeed = MoveTowards(currentSpeed, request.Speed,
                            request.Acceleration * request.DeltaTime);
                        velocity.Linear += request.Direction * (targetSpeed - currentSpeed);
                    }
                    break;

                case ReleasedBlockInteractionType.Suction:
                    float3 local = math.rotate(request.InverseRotation,
                        position - request.Origin - request.BoxOffset);
                    if (math.any(math.abs(local) > request.HalfSize))
                        break;

                    float3 direction = request.Origin - position;
                    direction.z = 0f;
                    float distance = math.length(direction);
                    if (distance < request.DestroyRadius)
                    {
                        simulate.ValueRW = false;
                        CommandBuffer.DestroyEntity(sortKey, entity);
                        SuckedCount.Value++;
                        return;
                    }

                    direction /= distance;
                    block.StableFrames = 0;
                    simulate.ValueRW = true;
                    gravity.Value = 1f;
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
