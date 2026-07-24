using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Physics.Systems;

namespace Unity.Physics.Stateful
{
    /// <summary>
    /// This system converts stream of TriggerEvents to StatefulTriggerEvents that can be stored in a Dynamic Buffer.
    /// In order for this conversion, it is required to:
    ///    1) Use the 'Raise Trigger Events' option of the 'Collision Response' property on a <see cref="PhysicsShapeAuthoring"/> component, and
    ///    2) Add a <see cref="StatefulTriggerEventBufferAuthoring"/> component to that entity
    /// or, if this is desired on a Character Controller:
    ///    1) Tick the 'Raise Trigger Events' flag on the <see cref="CharacterControllerAuthoring"/> component.
    ///       Note: the Character Controller will not become a trigger, it will raise events when overlapping with one
    /// </summary>
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(PhysicsSimulationGroup))]
    [BurstCompile]
    public partial struct StatefulTriggerEventBufferSystem : ISystem
    {
        private StatefulSimulationEventBuffers<StatefulTriggerEvent> _statefulEventBuffers;
        private ComponentHandles _componentHandles;
        private EntityQuery _triggerEventQuery;

        private struct ComponentHandles
        {
            public ComponentLookup<StatefulTriggerEventExclude> EventExcludes;
            public BufferLookup<StatefulTriggerEvent> EventBuffers;

            public ComponentHandles(ref SystemState systemState)
            {
                EventExcludes = systemState.GetComponentLookup<StatefulTriggerEventExclude>(true);
                EventBuffers = systemState.GetBufferLookup<StatefulTriggerEvent>();
            }

            public void Update(ref SystemState systemState)
            {
                EventExcludes.Update(ref systemState);
                EventBuffers.Update(ref systemState);
            }
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            EntityQueryBuilder builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<StatefulTriggerEvent>()
                .WithNone<StatefulTriggerEventExclude>();

            _statefulEventBuffers = new StatefulSimulationEventBuffers<StatefulTriggerEvent>();
            _statefulEventBuffers.AllocateBuffers();

            _triggerEventQuery = state.GetEntityQuery(builder);
            state.RequireForUpdate(_triggerEventQuery);

            _componentHandles = new ComponentHandles(ref state);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            _statefulEventBuffers.Dispose();
        }

        [BurstCompile]
        public partial struct ClearTriggerEventDynamicBufferJob : IJobEntity
        {
            public void Execute(ref DynamicBuffer<StatefulTriggerEvent> eventBuffer) => eventBuffer.Clear();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _componentHandles.Update(ref state);

            state.Dependency = new ClearTriggerEventDynamicBufferJob()
                .ScheduleParallel(_triggerEventQuery, state.Dependency);

            _statefulEventBuffers.SwapBuffers();

            NativeList<StatefulTriggerEvent> currentEvents = _statefulEventBuffers.Current;
            NativeList<StatefulTriggerEvent> previousEvents = _statefulEventBuffers.Previous;

            state.Dependency = new StatefulEventCollectionJobs.CollectTriggerEvents
            {
                TriggerEvents = currentEvents
            }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);

            state.Dependency = new StatefulEventCollectionJobs
                .ConvertEventStreamToDynamicBufferJob<StatefulTriggerEvent, StatefulTriggerEventExclude>
            {
                CurrentEvents = currentEvents,
                PreviousEvents = previousEvents,
                EventBuffers = _componentHandles.EventBuffers,

                UseExcludeComponent = true,
                EventExcludeLookup = _componentHandles.EventExcludes
            }.Schedule(state.Dependency);
        }
    }
}
