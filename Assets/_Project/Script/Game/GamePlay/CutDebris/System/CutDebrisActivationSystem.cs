using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

namespace Crusher
{
    [UpdateInGroup(typeof(BeforePhysicsSystemGroup))]
    [UpdateAfter(typeof(SawDeviceSystem))]
    public partial class CutDebrisActivationSystem : SystemBase
    {
        private EntityQuery _pendingQuery;

        protected override void OnCreate()
        {
            _pendingQuery = GetEntityQuery(
                ComponentType.ReadOnly<CutDebrisPendingActivation>());
        }

        protected override void OnUpdate()
        {
            if (_pendingQuery.IsEmptyIgnoreFilter)
                return;

            using NativeArray<Entity> entities = _pendingQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                if (!EntityManager.IsComponentEnabled<CutDebrisPendingActivation>(entity) ||
                    EntityManager.IsComponentEnabled<Simulate>(entity))
                    continue;

                CutDebrisPendingActivation pending =
                    EntityManager.GetComponentData<CutDebrisPendingActivation>(entity);
                EntityManager.SetComponentData(entity, new PhysicsVelocity
                {
                    Linear = pending.InitialVelocity
                });
                EntityManager.SetComponentEnabled<CutDebrisPendingActivation>(entity, false);
                EntityManager.SetComponentEnabled<Simulate>(entity, true);
            }
        }
    }
}
