using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace Crusher
{
    [UpdateInGroup(typeof(PhysicsSimulationGroup), OrderFirst = true)]
    public partial class CutDebrisActivationSystem : SystemBase
    {
        private EntityQuery _pendingQuery;

        protected override void OnCreate()
        {
            _pendingQuery = GetEntityQuery(
                ComponentType.ReadOnly<CutDebrisPendingActivation>(),
                ComponentType.ReadOnly<LocalTransform>());
            RequireForUpdate<PhysicsWorldSingleton>();
        }

        protected override void OnUpdate()
        {
            if (_pendingQuery.IsEmptyIgnoreFilter)
                return;

            CollisionWorld collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            using NativeArray<Entity> entities = _pendingQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                if (!EntityManager.IsComponentEnabled<CutDebrisPendingActivation>(entity) ||
                    EntityManager.IsComponentEnabled<Simulate>(entity))
                    continue;

                LocalTransform transform = EntityManager.GetComponentData<LocalTransform>(entity);
                CutDebrisPendingActivation pending =
                    EntityManager.GetComponentData<CutDebrisPendingActivation>(entity);
                ColliderDistanceInput input = new(
                    pending.Collider, 0f,
                    new RigidTransform(transform.Rotation, transform.Position), transform.Scale);
                if (collisionWorld.CalculateDistance(input))
                    continue;

                EntityManager.AddComponentData(entity, new PhysicsCollider { Value = pending.Collider });
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
