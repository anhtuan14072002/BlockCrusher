using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace Crusher
{
    [UpdateInGroup(typeof(BeforePhysicsSystemGroup))]
    [UpdateAfter(typeof(SawDeviceSystem))]
    public partial struct SuctionSystem : ISystem
    {
        private EntityQuery _blocks;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _blocks = SystemAPI.QueryBuilder()
                .WithAllRW<LocalTransform>()
                .WithAllRW<PhysicsCollider>()
                .WithAllRW<PhysicsVelocity>()
                .WithAllRW<PhysicsGravityFactor>()
                .WithAllRW<ReleasedBlockComponent>()
                .WithAllRW<Simulate>()
                .WithAllRW<SuctionTransit>()
                .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                .Build();
            state.RequireForUpdate<SuctionComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            SuctionComponent suction = SystemAPI.GetSingleton<SuctionComponent>();
            if (suction.Active == 0)
                return;

            DynamicBuffer<SuctionPathPoint> pathBuffer = SystemAPI.GetSingletonBuffer<SuctionPathPoint>(true);
            if (pathBuffer.Length < 2)
                return;

            EntityCommandBuffer.ParallelWriter commandBuffer = SystemAPI
                .GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged)
                .AsParallelWriter();
            state.Dependency = new SuctionJob
            {
                Path = pathBuffer.AsNativeArray(),
                Speed = suction.Speed,
                DeltaTime = SystemAPI.Time.DeltaTime,
                CommandBuffer = commandBuffer
            }.ScheduleParallel(_blocks, state.Dependency);
        }

        private static void AdvanceAlongPath(NativeArray<SuctionPathPoint> path,
            float lockedZ, ref float3 position, ref int pathIndex, float distanceToMove)
        {
            while (distanceToMove > 0f && pathIndex < path.Length)
            {
                float3 target = path[pathIndex].Value;
                target.z = lockedZ;
                float3 delta = target - position;
                float distance = math.length(delta);
                if (distance > distanceToMove && distance > 0.0001f)
                {
                    position += delta * (distanceToMove / distance);
                    return;
                }

                position = target;
                distanceToMove -= distance;
                pathIndex++;
            }
        }

#if UNITY_EDITOR
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ValidatePathAdvance()
        {
            NativeArray<SuctionPathPoint> path = new(2, Allocator.Temp);
            path[0] = new SuctionPathPoint { Value = float3.zero };
            path[1] = new SuctionPathPoint { Value = new float3(0f, 2f, 0f) };
            float3 position = new(-1f, 0f, 0f);
            int pathIndex = 0;
            AdvanceAlongPath(path, 0f, ref position, ref pathIndex, 2f);
            UnityEngine.Debug.Assert(pathIndex == 1 && math.distance(position, new float3(0f, 1f, 0f)) < 0.001f,
                "Suction path advance regression.");
            path.Dispose();
        }
#endif

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        private partial struct SuctionJob : IJobEntity
        {
            [ReadOnly] public NativeArray<SuctionPathPoint> Path;
            public float Speed;
            public float DeltaTime;
            public EntityCommandBuffer.ParallelWriter CommandBuffer;

            private void Execute([EntityIndexInQuery] int sortKey, Entity entity,
                ref LocalTransform transform, ref PhysicsCollider collider, ref PhysicsVelocity velocity,
                ref PhysicsGravityFactor gravity, ref ReleasedBlockComponent block,
                EnabledRefRW<Simulate> simulate, EnabledRefRW<SuctionTransit> transit)
            {
                if (!transit.ValueRO)
                {
                    transit.ValueRW = true;
                    simulate.ValueRW = false;
                    collider.Value = default;
                    velocity = PhysicsVelocity.Zero;
                    gravity.Value = 0f;
                    block.SuctionPathIndex = 0;
                }

                int pathIndex = block.SuctionPathIndex;
                float3 position = transform.Position;
                AdvanceAlongPath(Path, block.LockedZ, ref position, ref pathIndex, Speed * DeltaTime);

                if (pathIndex >= Path.Length)
                {
                    CommandBuffer.DestroyEntity(sortKey, entity);
                    return;
                }

                block.SuctionPathIndex = (byte)pathIndex;
                transform.Position = position;
            }
        }
    }
}
