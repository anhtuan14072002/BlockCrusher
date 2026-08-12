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
    public partial class CutDebrisActivationSystem : SystemBase
    {
        private const float SawContactArmMargin = 0.02f;

        private EntityQuery _pendingQuery;
        private EntityQuery _sawQuery;

        protected override void OnCreate()
        {
            _pendingQuery = GetEntityQuery(
                ComponentType.ReadOnly<CutDebrisPendingActivation>());
            _sawQuery = GetEntityQuery(
                ComponentType.ReadOnly<SawDeviceTag>(), ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<PhysicsCollider>());
        }

        protected override void OnUpdate()
        {
            if (_pendingQuery.IsEmptyIgnoreFilter)
                return;

            bool hasSaw = _sawQuery.CalculateEntityCount() == 1;
            Aabb sawAabb = default;
            if (hasSaw)
            {
                Entity sawEntity = _sawQuery.GetSingletonEntity();
                LocalTransform sawTransform = EntityManager.GetComponentData<LocalTransform>(sawEntity);
                PhysicsCollider sawCollider = EntityManager.GetComponentData<PhysicsCollider>(sawEntity);
                sawAabb = sawCollider.Value.Value.CalculateAabb(
                    new RigidTransform(sawTransform.Rotation, sawTransform.Position), sawTransform.Scale);
                sawAabb.Expand(SawContactArmMargin);
            }

            using NativeArray<Entity> entities = _pendingQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                if (!EntityManager.IsComponentEnabled<CutDebrisPendingActivation>(entity))
                    continue;

                CutDebrisPendingActivation pending =
                    EntityManager.GetComponentData<CutDebrisPendingActivation>(entity);
                if (EntityManager.IsComponentEnabled<Simulate>(entity))
                {
                    if (!hasSaw)
                        continue;

                    LocalTransform debrisTransform = EntityManager.GetComponentData<LocalTransform>(entity);
                    PhysicsCollider debrisCollider = EntityManager.GetComponentData<PhysicsCollider>(entity);
                    Aabb debrisAabb = debrisCollider.Value.Value.CalculateAabb(
                        new RigidTransform(debrisTransform.Rotation, debrisTransform.Position),
                        debrisTransform.Scale);
                    if (sawAabb.Overlaps(debrisAabb))
                        continue;

                    EntityManager.SetComponentData(entity,
                        new PhysicsCollider { Value = pending.ContactCollider });
                    EntityManager.SetComponentEnabled<CutDebrisPendingActivation>(entity, false);
                    continue;
                }

                EntityManager.SetComponentData(entity, new PhysicsVelocity
                {
                    Linear = pending.InitialVelocity
                });
                EntityManager.SetComponentEnabled<Simulate>(entity, true);
            }
        }
    }
}
