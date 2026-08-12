using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Serialization;
using PhysicsColliderBlob = Unity.Physics.Collider;

namespace Crusher
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(PhysicsShapeAuthoring), typeof(PhysicsBodyAuthoring))]
    public sealed class SawAuthoring : MonoBehaviour
    {
        [SerializeField] private CuttingDeviceKind _deviceKind;
        [SerializeField] private float _bladeSpinDirection = 1f;
        [SerializeField] private float _bladeSpinSpeed = 600f;
        [SerializeField] private Vector3 _bladeSpinAxis = Vector3.forward;
        [SerializeField, Min(0.01f)] private float _cutSweepStep = 0.08f;
        [FormerlySerializedAs("_maxBlockVelocity")]
        [SerializeField, Min(0f)] private float _blockEjectSpeed = 6f;
        [SerializeField, Min(0f)] private float _obstacleDamagePerSecond = 1f;
        [SerializeField] private bool _canBreakStone;
        [SerializeField, Min(0f)] private float _obstacleBounceDistance = 0.08f;
        [SerializeField] private Transform _bladeVisual;
        [SerializeField, Min(0)] private int _bladeSubMeshIndex = 1;

        private BlobAssetReference<PhysicsColliderBlob> _runtimeCollider;
        private BlobAssetReference<PhysicsColliderBlob> _obstacleQueryCollider;
        private Vector3 _obstacleQueryScale;
        private World _runtimeWorld;
        private Entity _runtimeEntity;

        public CuttingDeviceKind DeviceKind => _deviceKind;
        public Transform BladeVisual => _bladeVisual != null ? _bladeVisual : transform;
        public float SpinSpeed => _bladeSpinSpeed * Mathf.Sign(_bladeSpinDirection);
        public Vector3 SpinAxis => _bladeSpinAxis;
        public bool CanBreakStone => _canBreakStone;
        public float ObstacleBounceDistance => _obstacleBounceDistance;

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
            if (_obstacleQueryCollider.IsCreated)
                _obstacleQueryCollider.Dispose();
        }

        public void SetVisualActive(bool active)
        {
            Transform visualRoot = _bladeVisual != null ? _bladeVisual : transform;
            Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].enabled = active;
        }

        public Vector3 ClampObstacleTarget(
            Vector3 rootFrom, Vector3 rootTo, float deltaTime, out bool hitStone)
        {
            hitStone = false;
            if (!EnsureObstacleQueryCollider())
                return rootTo;

            Vector3 movement = rootTo - rootFrom;
            Vector3 cutterFrom = transform.position;
            Vector3 cutterTo = cutterFrom + movement;
            Vector3 clampedCutter = LevelObstacle.ClampSawTarget(
                cutterFrom, cutterTo, _obstacleQueryCollider, transform.rotation,
                _canBreakStone, _obstacleDamagePerSecond * deltaTime,
                out bool damagedObstacle, out bool hitBreakableObstacle);
            hitStone = damagedObstacle || hitBreakableObstacle;
            Vector3 clampedRoot = rootTo + clampedCutter - cutterTo;
            clampedRoot.z = rootFrom.z;
            return clampedRoot;
        }

        private void CreateRuntimeEntityWhenNotBaked()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            EntityManager entityManager = world.EntityManager;
            ComponentType deviceTag = _deviceKind == CuttingDeviceKind.Saw
                ? ComponentType.ReadOnly<SawDeviceTag>()
                : ComponentType.ReadOnly<DrillDeviceTag>();
            EntityQuery bakedQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<SawComponent>(), deviceTag);
            bool hasBakedSaw = !bakedQuery.IsEmptyIgnoreFilter;
            bakedQuery.Dispose();
            if (hasBakedSaw)
                return;

            Mesh mesh = GetComponent<MeshFilter>().sharedMesh;
            int cutSubMeshIndex = GetCutSubMeshIndex();
            CollisionResponsePolicy collisionResponse = _deviceKind == CuttingDeviceKind.Saw
                ? CollisionResponsePolicy.Collide
                : CollisionResponsePolicy.RaiseTriggerEvents;
            CollisionFilter collisionFilter = _deviceKind == CuttingDeviceKind.Saw
                ? new CollisionFilter
                {
                    BelongsTo = DeviceMeshCollider.SawCategory,
                    CollidesWith = uint.MaxValue
                }
                : CollisionFilter.Default;
            if (!DeviceMeshCollider.TryCreate(mesh, transform.lossyScale,
                    collisionResponse, collisionFilter, out _runtimeCollider, cutSubMeshIndex))
            {
                Debug.LogError("Cut submesh could not be read from the device mesh.", this);
                return;
            }

            _runtimeWorld = world;
            _runtimeEntity = entityManager.CreateEntity(
                typeof(LocalTransform), typeof(SawComponent), typeof(PhysicsCollider),
                typeof(DeviceBodyComponent), typeof(PhysicsMass), typeof(PhysicsVelocity),
                typeof(PhysicsGravityFactor), typeof(Simulate));
            entityManager.SetComponentData(_runtimeEntity,
                LocalTransform.FromPositionRotationScale(transform.position, transform.rotation, 1f));
            entityManager.SetComponentData(_runtimeEntity, CreateComponent(mesh, transform));
            entityManager.SetComponentData(_runtimeEntity, CreateBodyComponent(transform));
            entityManager.SetComponentData(_runtimeEntity, new PhysicsCollider { Value = _runtimeCollider });
            entityManager.SetComponentData(_runtimeEntity,
                PhysicsMass.CreateKinematic(_runtimeCollider.Value.MassProperties));
            entityManager.SetComponentData(_runtimeEntity, PhysicsVelocity.Zero);
            entityManager.SetComponentData(_runtimeEntity, new PhysicsGravityFactor { Value = 0f });
            if (_deviceKind == CuttingDeviceKind.Saw)
                entityManager.AddComponent<SawDeviceTag>(_runtimeEntity);
            else
                entityManager.AddComponent<DrillDeviceTag>(_runtimeEntity);
            entityManager.SetComponentEnabled<Simulate>(
                _runtimeEntity, _deviceKind == CuttingDeviceKind.Saw);
            entityManager.AddSharedComponent(_runtimeEntity, new PhysicsWorldIndex(0));
            entityManager.SetName(_runtimeEntity, $"{_deviceKind} Device");
        }

        private bool EnsureObstacleQueryCollider()
        {
            Vector3 scale = transform.lossyScale;
            if (_obstacleQueryCollider.IsCreated && scale == _obstacleQueryScale)
                return true;

            if (_obstacleQueryCollider.IsCreated)
                _obstacleQueryCollider.Dispose();

            _obstacleQueryScale = scale;
            return DeviceMeshCollider.TryCreate(
                GetComponent<MeshFilter>().sharedMesh, scale,
                CollisionResponsePolicy.RaiseTriggerEvents, out _obstacleQueryCollider,
                GetCutSubMeshIndex());
        }

        private SawComponent CreateComponent(Mesh mesh, Transform sourceTransform)
        {
            Vector3 scale = sourceTransform.lossyScale;
            return new SawComponent
            {
                CutMesh = mesh,
                CutSubMeshIndex = GetCutSubMeshIndex(),
                Scale = new float3(scale.x, scale.y, scale.z),
                CutSweepStep = _cutSweepStep,
                BlockEjectSpeed = _blockEjectSpeed
            };
        }

        private int GetCutSubMeshIndex()
        {
            return _deviceKind == CuttingDeviceKind.Saw ? _bladeSubMeshIndex : -1;
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

        private sealed class SawBaker : Baker<SawAuthoring>
        {
            public override void Bake(SawAuthoring authoring)
            {
                Mesh mesh = authoring.GetComponent<MeshFilter>().sharedMesh;
                DependsOn(mesh);
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, authoring.CreateComponent(mesh, authoring.transform));
                AddComponent(entity, CreateBodyComponent(authoring.transform));
                if (authoring._deviceKind == CuttingDeviceKind.Saw)
                    AddComponent<SawDeviceTag>(entity);
                else
                    AddComponent<DrillDeviceTag>(entity);
            }
        }
    }
}
