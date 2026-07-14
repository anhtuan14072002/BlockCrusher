using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

internal enum ReleasedBlockInteractionType : byte
{
    Conveyor,
    Clear,
    Suction
}

internal struct ReleasedBlockInteractionRequest
{
    public ReleasedBlockInteractionType Type;
    public float3 BoundsMin;
    public float3 BoundsMax;
    public float3 Origin;
    public quaternion InverseRotation;
    public float3 HalfSize;
    public float3 BoxOffset;
    public float3 Direction;
    public float Speed;
    public float Acceleration;
    public float Force;
    public float MaxVelocity;
    public float ArrivalDamping;
    public float DestroyRadius;
    public float DeltaTime;
}

internal static class ReleasedBlockInteractionQueue
{
    private static readonly List<ReleasedBlockInteractionRequest> Requests = new(8);

    public static int Count => Requests.Count;

    public static void Enqueue(in ReleasedBlockInteractionRequest request)
    {
        Requests.Add(request);
    }

    public static void CopyToAndClear(NativeArray<ReleasedBlockInteractionRequest> destination)
    {
        for (int i = 0; i < Requests.Count; i++)
            destination[i] = Requests[i];
        Requests.Clear();
    }
}

[UpdateInGroup(typeof(BeforePhysicsSystemGroup))]
public partial struct ReleasedBlockInteractionSystem : ISystem
{
    private EntityQuery _query;
    private NativeReference<int> _suckedCount;
    private JobHandle _lastInteractionHandle;
    private bool _hasPendingSuckedCount;

    public void OnCreate(ref SystemState state)
    {
        _query = SystemAPI.QueryBuilder()
            .WithAllRW<PhysicsVelocity>()
            .WithAllRW<PhysicsGravityFactor>()
            .WithAllRW<ReleasedBlockComponent>()
            .WithAllRW<Simulate>()
            .WithAll<LocalTransform>()
            .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
            .Build();
        _suckedCount = new NativeReference<int>(Allocator.Persistent);
    }

    public void OnDestroy(ref SystemState state)
    {
        FlushSuckedCount();
        if (_suckedCount.IsCreated)
            _suckedCount.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        FlushSuckedCount();

        JobHandle dependency = state.Dependency;
        int requestCount = ReleasedBlockInteractionQueue.Count;
        if (requestCount > 0)
        {
            NativeArray<ReleasedBlockInteractionRequest> requests =
                new NativeArray<ReleasedBlockInteractionRequest>(requestCount, Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory);
            ReleasedBlockInteractionQueue.CopyToAndClear(requests);
            _suckedCount.Value = 0;

            EntityCommandBuffer.ParallelWriter commandBuffer = SystemAPI
                .GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged)
                .AsParallelWriter();

            JobHandle interactionHandle = new InteractionJob
            {
                Requests = requests,
                CommandBuffer = commandBuffer,
                SuckedCount = _suckedCount
            }.Schedule(_query, dependency);

            dependency = JobHandle.CombineDependencies(interactionHandle, requests.Dispose(interactionHandle));
            _lastInteractionHandle = interactionHandle;
            _hasPendingSuckedCount = true;
        }

        for (int i = TextureBlockSpawner.ActiveSpawnerCount - 1; i >= 0; i--)
        {
            TextureBlockSpawner spawner = TextureBlockSpawner.GetActiveSpawner(i);
            if (spawner == null)
            {
                TextureBlockSpawner.RemoveActiveSpawnerAt(i);
                continue;
            }
            if (spawner.TryCreatePendingSawPushJob(out TextureBlockSpawner.SawPushJob sawPushJob))
                dependency = sawPushJob.ScheduleParallel(_query, dependency);
        }
        state.Dependency = dependency;
    }

    private void FlushSuckedCount()
    {
        if (!_hasPendingSuckedCount)
            return;
        _lastInteractionHandle.Complete();
        _hasPendingSuckedCount = false;
        int count = _suckedCount.Value;
        if (count > 0)
            TextureBlockSpawner.NotifyBlocksSucked(count);
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    private partial struct InteractionJob : IJobEntity
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
}

[BurstCompile]
[UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
public partial struct ReleasedBlockPlanarConstraintSystem : ISystem
{
    private EntityQuery _query;

    public void OnCreate(ref SystemState state)
    {
        _query = SystemAPI.QueryBuilder()
            .WithAllRW<LocalTransform>()
            .WithAllRW<PhysicsVelocity>()
            .WithAllRW<PhysicsGravityFactor>()
            .WithAllRW<ReleasedBlockComponent>()
            .WithAllRW<Simulate>()
            .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
            .Build();
        state.RequireForUpdate(_query);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new PlanarConstraintJob().ScheduleParallel(_query, state.Dependency);
    }

    [BurstCompile]
    private partial struct PlanarConstraintJob : IJobEntity
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
}
