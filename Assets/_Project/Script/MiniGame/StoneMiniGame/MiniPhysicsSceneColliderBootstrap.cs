using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace Wizard
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.BoxCollider))]
    public sealed class MiniPhysicsSceneColliderBootstrap : MonoBehaviour
    {
        private Entity _entity;

        private void Start()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;

            UnityEngine.BoxCollider box = GetComponent<UnityEngine.BoxCollider>();
            Vector3 scale = transform.lossyScale;
            float3 size = new float3(
                math.abs(box.size.x * scale.x),
                math.abs(box.size.y * scale.y),
                math.abs(box.size.z * scale.z));

            BlobAssetReference<Unity.Physics.Collider> collider = Unity.Physics.BoxCollider.Create(
                new BoxGeometry
                {
                    Center = float3.zero,
                    Orientation = quaternion.identity,
                    Size = size,
                    BevelRadius = 0f
                }, CollisionFilter.Default);

            EntityManager entityManager = world.EntityManager;
            _entity = entityManager.CreateEntity(typeof(LocalTransform), typeof(PhysicsCollider));
            entityManager.SetComponentData(_entity, LocalTransform.FromPositionRotationScale(
                transform.TransformPoint(box.center), transform.rotation, 1f));
            entityManager.SetComponentData(_entity, new PhysicsCollider { Value = collider });
            entityManager.AddSharedComponent(_entity, new PhysicsWorldIndex(0));
        }

        private void OnDestroy()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated && world.EntityManager.Exists(_entity))
                world.EntityManager.DestroyEntity(_entity);
        }
    }
}
