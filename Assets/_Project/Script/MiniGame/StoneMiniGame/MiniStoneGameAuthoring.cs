using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Stateful;
using Unity.Transforms;
using UnityEngine;

namespace Wizard
{
    public class MiniStoneGameAuthoring : MonoBehaviour
    {
        [SerializeField] private GameObject stonePrefab;
        public int amount;
        public int spawnedAmount;
        public float elapsedTime;

        public int minMapFew;
        public int maxMapFew;
        public int minMapMedium;
        public int maxMapMedium;
        public int minMapMany;
        public int maxMapMany;

        public float ElapsedTime => elapsedTime;
        public int SpawnedAmount => spawnedAmount;

        private BlobAssetReference<Unity.Physics.Collider> _stoneCollider;
        private Entity _renderEntity;
        private UnityEngine.Material _runtimeMaterial;

        private void Start()
        {
            MeshFilter meshFilter = stonePrefab.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = stonePrefab.GetComponent<MeshRenderer>();
            _runtimeMaterial = new UnityEngine.Material(meshRenderer.sharedMaterial)
            {
                enableInstancing = true
            };
            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _renderEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(_renderEntity, new MiniStoneRenderData
            {
                Mesh = meshFilter.sharedMesh,
                Material = _runtimeMaterial
            });

            float radius = 0.5f;
            _stoneCollider = Unity.Physics.SphereCollider.Create(new SphereGeometry
            {
                Center = float3.zero,
                Radius = radius
            }, CollisionFilter.Default);
        }

        public bool TrySpawnStone(float3 position, float scale)
        {
            if (amount <= 0 || !_stoneCollider.IsCreated) return false;

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            Entity entity = entityManager.CreateEntity(
                typeof(LocalTransform), typeof(LocalToWorld), typeof(PhysicsCollider),
                typeof(PhysicsMass), typeof(PhysicsVelocity), typeof(PhysicsDamping),
                typeof(PhysicsGravityFactor), typeof(Simulate), typeof(MiniStoneTag));

            entityManager.SetComponentData(entity,
                LocalTransform.FromPositionRotationScale(position, quaternion.identity, scale));
            entityManager.SetComponentData(entity, new PhysicsCollider { Value = _stoneCollider });
            entityManager.SetComponentData(entity,
                PhysicsMass.CreateDynamic(_stoneCollider.Value.MassProperties, 1f));
            entityManager.SetComponentData(entity, new PhysicsVelocity());
            entityManager.SetComponentData(entity, new PhysicsDamping { Linear = 0.01f, Angular = 0.05f });
            entityManager.SetComponentData(entity, new PhysicsGravityFactor { Value = 1f });
            entityManager.AddSharedComponent(entity, new PhysicsWorldIndex(0));
            entityManager.AddBuffer<MiniCheckJumpComponent>(entity);
            entityManager.AddBuffer<MiniCheckMultiComponent>(entity);

            amount--;
            spawnedAmount++;
            return true;
        }

        private void OnDestroy()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated && world.EntityManager.Exists(_renderEntity))
                world.EntityManager.DestroyEntity(_renderEntity);
            if (_stoneCollider.IsCreated)
                _stoneCollider.Dispose();
            if (_runtimeMaterial != null)
                Destroy(_runtimeMaterial);
        }

        public bool TryConsumeStone(out GameObject prefab)
        {
            prefab = stonePrefab;
            if (amount <= 0 || stonePrefab == null) return false;

            amount--;
            spawnedAmount++;
            return true;
        }
        
        class Baker : Baker<MiniStoneGameAuthoring>
        {
            public override void Bake(MiniStoneGameAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new MiniStoneComponent
                {
                    EntityStone = GetEntity(authoring.stonePrefab, TransformUsageFlags.Dynamic),
                    Amount = authoring.amount,
                    ElapsedTime = authoring.elapsedTime,
                    
                    MinMapFew = authoring.minMapFew,
                    MaxMapFew = authoring.maxMapFew,
                    MinMapMedium = authoring.minMapMedium,
                    MaxMapMedium = authoring.maxMapMedium,  
                    MinMapMany = authoring.minMapMany,
                    MaxMapMany = authoring.maxMapMany,
                    
                });
                AddBuffer<StatefulTriggerEvent>(entity);
            }
        }
    }
}
