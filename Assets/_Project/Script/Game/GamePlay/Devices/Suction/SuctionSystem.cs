using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace Crusher
{
    [BurstCompile]
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    public partial struct SuctionContactSystem : ISystem
    {
        private ComponentLookup<SuctionDeviceTag> _suctionLookup;
        private ComponentLookup<CutDebrisComponent> _debrisLookup;
        private ComponentLookup<ReleasedBlockComponent> _releasedBlockLookup;

        public void OnCreate(ref SystemState state)
        {
            _suctionLookup = state.GetComponentLookup<SuctionDeviceTag>(true);
            _debrisLookup = state.GetComponentLookup<CutDebrisComponent>(true);
            _releasedBlockLookup = state.GetComponentLookup<ReleasedBlockComponent>(true);
            state.RequireForUpdate<SimulationSingleton>();
            state.RequireForUpdate<SuctionComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.GetSingleton<SuctionComponent>().Active == 0)
                return;

            _suctionLookup.Update(ref state);
            _debrisLookup.Update(ref state);
            _releasedBlockLookup.Update(ref state);
            EntityCommandBuffer commandBuffer = SystemAPI
                .GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            state.Dependency = new TriggerJob
            {
                SuctionLookup = _suctionLookup,
                DebrisLookup = _debrisLookup,
                ReleasedBlockLookup = _releasedBlockLookup,
                CommandBuffer = commandBuffer
            }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
        }

        [BurstCompile]
        private struct TriggerJob : ITriggerEventsJob
        {
            [ReadOnly] public ComponentLookup<SuctionDeviceTag> SuctionLookup;
            [ReadOnly] public ComponentLookup<CutDebrisComponent> DebrisLookup;
            [ReadOnly] public ComponentLookup<ReleasedBlockComponent> ReleasedBlockLookup;
            public EntityCommandBuffer CommandBuffer;

            public void Execute(TriggerEvent triggerEvent)
            {
                Entity debris = Entity.Null;
                if (SuctionLookup.HasComponent(triggerEvent.EntityA) &&
                    DebrisLookup.HasComponent(triggerEvent.EntityB))
                {
                    debris = triggerEvent.EntityB;
                }
                else if (SuctionLookup.HasComponent(triggerEvent.EntityB) &&
                         DebrisLookup.HasComponent(triggerEvent.EntityA))
                {
                    debris = triggerEvent.EntityA;
                }

                if (debris == Entity.Null)
                {
                    Entity releasedBlock = Entity.Null;
                    if (SuctionLookup.HasComponent(triggerEvent.EntityA) &&
                        ReleasedBlockLookup.HasComponent(triggerEvent.EntityB))
                    {
                        releasedBlock = triggerEvent.EntityB;
                    }
                    else if (SuctionLookup.HasComponent(triggerEvent.EntityB) &&
                             ReleasedBlockLookup.HasComponent(triggerEvent.EntityA))
                    {
                        releasedBlock = triggerEvent.EntityA;
                    }

                    if (releasedBlock == Entity.Null)
                        return;

                    CommandBuffer.SetComponent(releasedBlock, new SuctionTransit { Distance = 0f });
                    CommandBuffer.SetComponentEnabled<SuctionTransit>(releasedBlock, true);
                    CommandBuffer.SetComponentEnabled<ReleasedBlockSolidConstraint>(releasedBlock, false);
                    CommandBuffer.SetComponentEnabled<Simulate>(releasedBlock, false);
                    return;
                }

                CommandBuffer.SetComponentEnabled<CutDebrisSuctionTransit>(debris, true);
                CommandBuffer.SetComponentEnabled<Simulate>(debris, false);
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct SuctionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SuctionComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            DynamicBuffer<SuctionPathPoint> path = SystemAPI.GetSingletonBuffer<SuctionPathPoint>(true);
            if (path.Length < 2)
                return;

            SuctionComponent suction = SystemAPI.GetSingleton<SuctionComponent>();
            EntityCommandBuffer.ParallelWriter commandBuffer = SystemAPI
                .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged)
                .AsParallelWriter();
            state.Dependency = new TransitJob
            {
                Path = path.AsNativeArray(),
                Speed = suction.Speed,
                TargetScale = suction.TargetScale,
                ShrinkSpeed = suction.ShrinkSpeed,
                DeltaTime = SystemAPI.Time.DeltaTime,
                CommandBuffer = commandBuffer
            }.ScheduleParallel(state.Dependency);
            state.Dependency = new ReleasedBlockTransitJob
            {
                Path = path.AsNativeArray(),
                Speed = suction.Speed,
                ShrinkSpeed = suction.ShrinkSpeed,
                DeltaTime = SystemAPI.Time.DeltaTime,
                CommandBuffer = commandBuffer
            }.ScheduleParallel(state.Dependency);
        }

        private static float3 SamplePath(NativeArray<SuctionPathPoint> path,
            float distance, float lockedZ, out bool reachedRoot)
        {
            for (int i = 1; i < path.Length; i++)
            {
                float3 from = path[i - 1].Value;
                float3 to = path[i].Value;
                from.z = lockedZ;
                to.z = lockedZ;
                float segmentLength = math.distance(from, to);
                if (distance <= segmentLength && segmentLength > 0.0001f)
                {
                    reachedRoot = false;
                    return math.lerp(from, to, distance / segmentLength);
                }

                distance -= segmentLength;
            }

            reachedRoot = true;
            float3 root = path[path.Length - 1].Value;
            root.z = lockedZ;
            return root;
        }

#if UNITY_EDITOR
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ValidatePathSampling()
        {
            NativeArray<SuctionPathPoint> path = new(3, Allocator.Temp);
            path[0] = new SuctionPathPoint { Value = float3.zero };
            path[1] = new SuctionPathPoint { Value = new float3(1f, 0f, 0f) };
            path[2] = new SuctionPathPoint { Value = new float3(1f, 2f, 0f) };
            float3 position = SamplePath(path, 2f, 0f, out bool reachedRoot);
            UnityEngine.Debug.Assert(!reachedRoot && math.distance(position, new float3(1f, 1f, 0f)) < 0.001f,
                "Suction path sampling regression.");
            path.Dispose();
        }
#endif

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        private partial struct TransitJob : IJobEntity
        {
            [ReadOnly] public NativeArray<SuctionPathPoint> Path;
            public float Speed;
            public float TargetScale;
            public float ShrinkSpeed;
            public float DeltaTime;
            public EntityCommandBuffer.ParallelWriter CommandBuffer;

            private void Execute([EntityIndexInQuery] int sortKey, Entity entity,
                ref LocalTransform transform, ref CutDebrisComponent debris,
                in CutDebrisSuctionTransit transit)
            {
                debris.SuctionDistance += Speed * DeltaTime;
                transform.Position = SamplePath(
                    Path, debris.SuctionDistance, debris.LockedZ, out bool reachedRoot);
                transform.Scale = math.lerp(
                    transform.Scale, debris.BaseScale * TargetScale,
                    math.saturate(ShrinkSpeed * DeltaTime));
                if (reachedRoot)
                    CommandBuffer.DestroyEntity(sortKey, entity);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        private partial struct ReleasedBlockTransitJob : IJobEntity
        {
            [ReadOnly] public NativeArray<SuctionPathPoint> Path;
            public float Speed;
            public float ShrinkSpeed;
            public float DeltaTime;
            public EntityCommandBuffer.ParallelWriter CommandBuffer;

            private void Execute([EntityIndexInQuery] int sortKey, Entity entity,
                ref LocalTransform transform, ref SuctionTransit transit,
                in ReleasedBlockComponent block)
            {
                transit.Distance += Speed * DeltaTime;
                transform.Position = SamplePath(
                    Path, transit.Distance, block.LockedZ, out bool reachedRoot);
                transform.Scale = math.lerp(
                    transform.Scale, 0f, math.saturate(ShrinkSpeed * DeltaTime));
                if (reachedRoot)
                    CommandBuffer.DestroyEntity(sortKey, entity);
            }
        }
    }
}
