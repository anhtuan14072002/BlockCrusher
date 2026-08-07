using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using Unity.Transforms;
using UnityEngine;
using Collider = Unity.Physics.Collider;

namespace Crusher
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(PhysicsShapeAuthoring), typeof(PhysicsBodyAuthoring))]
    public sealed class SuctionAuthoring : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float _suctionSpeed = 8f;

        private World _runtimeWorld;
        private Entity _runtimeEntity;
        private BlobAssetReference<Collider> _runtimeCollider;

        private void Start()
        {
            CreateRuntimeEntityWhenNotBaked();
        }

        private void OnDestroy()
        {
            if (_runtimeWorld != null && _runtimeWorld.IsCreated && _runtimeEntity != Entity.Null &&
                _runtimeWorld.EntityManager.Exists(_runtimeEntity))
            {
                _runtimeWorld.EntityManager.DestroyEntity(_runtimeEntity);
            }

            if (_runtimeCollider.IsCreated)
                _runtimeCollider.Dispose();
        }

        public void SetVisualActive(bool active)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].enabled = active;
        }

        private void CreateRuntimeEntityWhenNotBaked()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            EntityManager entityManager = world.EntityManager;
            EntityQuery bakedQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<SuctionComponent>(), ComponentType.ReadOnly<SuctionDeviceTag>());
            bool hasBakedSuction = !bakedQuery.IsEmptyIgnoreFilter;
            bakedQuery.Dispose();
            if (hasBakedSuction)
                return;

            Mesh mesh = GetComponent<MeshFilter>().sharedMesh;
            if (!DeviceMeshCollider.TryCreate(mesh, transform.lossyScale, out _runtimeCollider))
            {
                Debug.LogError("Suction mesh must be readable so its DOTS mesh collider can be created.", this);
                return;
            }

            _runtimeWorld = world;
            _runtimeEntity = entityManager.CreateEntity(
                typeof(LocalTransform), typeof(SuctionComponent), typeof(DeviceBodyComponent),
                typeof(PhysicsCollider), typeof(PhysicsMass), typeof(PhysicsVelocity),
                typeof(PhysicsGravityFactor), typeof(Simulate), typeof(SuctionDeviceTag));
            entityManager.SetComponentData(_runtimeEntity,
                LocalTransform.FromPositionRotationScale(transform.position, transform.rotation, 1f));
            entityManager.SetComponentData(_runtimeEntity, new SuctionComponent { Speed = _suctionSpeed });
            entityManager.SetComponentData(_runtimeEntity, CreateBodyComponent(transform));
            entityManager.SetComponentData(_runtimeEntity, new PhysicsCollider { Value = _runtimeCollider });
            entityManager.SetComponentData(_runtimeEntity,
                PhysicsMass.CreateKinematic(_runtimeCollider.Value.MassProperties));
            entityManager.SetComponentData(_runtimeEntity, PhysicsVelocity.Zero);
            entityManager.SetComponentData(_runtimeEntity, new PhysicsGravityFactor { Value = 0f });
            entityManager.SetComponentEnabled<Simulate>(_runtimeEntity, false);
            entityManager.AddSharedComponent(_runtimeEntity, new PhysicsWorldIndex(0));
            entityManager.AddBuffer<SuctionPathPoint>(_runtimeEntity);
            entityManager.SetName(_runtimeEntity, "Suction Device");
        }

        private static DeviceBodyComponent CreateBodyComponent(Transform sourceTransform)
        {
            Quaternion rotation = sourceTransform.rotation;
            return new DeviceBodyComponent
            {
                TargetPosition = sourceTransform.position,
                TargetRotation = new quaternion(rotation.x, rotation.y, rotation.z, rotation.w),
                Initialized = 1
            };
        }

        private sealed class SuctionBaker : Baker<SuctionAuthoring>
        {
            public override void Bake(SuctionAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new SuctionComponent { Speed = authoring._suctionSpeed });
                AddComponent(entity, CreateBodyComponent(authoring.transform));
                AddComponent<SuctionDeviceTag>(entity);
                AddBuffer<SuctionPathPoint>(entity);
            }
        }
    }
}
