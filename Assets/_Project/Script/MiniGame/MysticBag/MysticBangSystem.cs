using Unity.Burst;
using Unity.Entities;
using Unity.Physics.Stateful;

namespace Wizard
{
    [BurstCompile]
    public partial struct MysticBagCollisionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MysticBagComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.TempJob);
            foreach (var (mysticBag, triggerEventsBuffer, bagEntity) in SystemAPI
                         .Query<RefRW<MysticBagComponent>, DynamicBuffer<StatefulTriggerEvent>>()
                         .WithEntityAccess())
            {
                foreach (var triggerEvent in triggerEventsBuffer)
                {
                    var otherEntity = triggerEvent.GetOtherEntity(bagEntity);

                    if (triggerEvent.State == StatefulEventState.Enter)
                    {
                        mysticBag.ValueRW.MysticStone++;

                        ecb.DestroyEntity(otherEntity);
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}