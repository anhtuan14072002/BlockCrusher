using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Crusher
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class CutDebrisSpawnSystem : SystemBase
    {
        private CutDebrisAuthoring _source;
        private EntityQuery _debrisQuery;
        private EntityArchetype _debrisArchetype;
        private readonly List<BlobAssetReference<Collider>> _colliders = new(16);
        private readonly List<PhysicsMass> _masses = new(16);
        private PhysicsDamping _damping;
        private float _gravityFactor;
        private int _generation = -1;
        private bool _runtimeReady;

        protected override void OnCreate()
        {
            _debrisQuery = GetEntityQuery(ComponentType.ReadOnly<CutDebrisComponent>());
        }

        protected override void OnUpdate()
        {
            CutDebrisAuthoring source = CutDebrisAuthoring.Active;
            if (_source != source)
            {
                ResetRuntime();
                _source = source;
            }

            if (_source == null)
                return;

            if (!_runtimeReady && !InitializeRuntime())
                return;

            if (_generation != _source.Generation)
            {
                DestroyDebris();
                DisposeVariantResources();
                _generation = _source.Generation;
            }

            if (!EnsureVariantResources())
                return;

            int available = _source.MaxActiveBlocks - _debrisQuery.CalculateEntityCount();
            if (available <= 0)
            {
                // Ponytail: the hard mobile cap drops overflow instead of retaining an unbounded delayed-spawn queue.
                _source.DiscardReady();
                return;
            }

            int spawnCount = math.min(available, math.min(_source.ReadyCount, _source.MaxSpawnsPerFrame));
            if (spawnCount == 0)
                return;

            using NativeArray<Entity> entities = new(spawnCount, Allocator.Temp);
            EntityManager.CreateEntity(_debrisArchetype, entities);
            for (int i = 0; i < spawnCount; i++)
            {
                Entity entity = entities[i];
                CutDebrisAuthoring.SpawnRequest request = _source.PopReady();
                CutDebrisAuthoring.VariantData variant = _source.GetVariant(request.VariantIndex);
                EntityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(
                    new float3(request.Position.x, request.Position.y, request.Position.z),
                    quaternion.identity, request.Scale));
                EntityManager.SetComponentData(entity, _masses[request.VariantIndex]);
                EntityManager.SetComponentData(entity, PhysicsVelocity.Zero);
                EntityManager.SetComponentData(entity, _damping);
                EntityManager.SetComponentData(entity, new PhysicsGravityFactor { Value = _gravityFactor });
                EntityManager.SetComponentData(entity, new CutDebrisComponent
                {
                    Color = new float4(request.Color.r / 255f, request.Color.g / 255f,
                        request.Color.b / 255f, request.Color.a / 255f),
                    RenderScale = new float3(
                        variant.RenderScale.x, variant.RenderScale.y, variant.RenderScale.z),
                    LockedZ = request.Position.z,
                    MaxPlanarSpeed = _source.MaxPlanarSpeed,
                    BaseScale = request.Scale,
                    VariantIndex = request.VariantIndex
                });
                EntityManager.SetComponentEnabled<CutDebrisSuctionTransit>(entity, false);
                EntityManager.SetComponentData(entity, new CutDebrisPendingActivation
                {
                    Collider = _colliders[request.VariantIndex],
                    InitialVelocity = new float3(
                        request.InitialVelocity.x, request.InitialVelocity.y, request.InitialVelocity.z)
                });
                EntityManager.SetComponentEnabled<CutDebrisPendingActivation>(entity, true);
                EntityManager.SetComponentEnabled<Simulate>(entity, false);
                EntityManager.SetName(entity, "Cut Debris");
            }
        }

        protected override void OnDestroy()
        {
            ResetRuntime();
        }

        private bool InitializeRuntime()
        {
            if (_source.PhysicsShape == null || _source.PhysicsBody == null)
                return false;

            _damping = new PhysicsDamping
            {
                Linear = _source.PhysicsBody.LinearDamping,
                Angular = _source.PhysicsBody.AngularDamping
            };
            _gravityFactor = _source.PhysicsBody.GravityFactor;
            _debrisArchetype = EntityManager.CreateArchetype(
                typeof(LocalTransform), typeof(PhysicsMass), typeof(PhysicsVelocity),
                typeof(PhysicsDamping), typeof(PhysicsGravityFactor), typeof(Simulate),
                typeof(PhysicsWorldIndex), typeof(CutDebrisComponent), typeof(CutDebrisSuctionTransit),
                typeof(CutDebrisPendingActivation));
            _runtimeReady = true;
            _generation = _source.Generation;
            return true;
        }

        private bool EnsureVariantResources()
        {
            while (_colliders.Count < _source.VariantCount)
            {
                BlobAssetReference<Collider> collider = BoxCollider.Create(
                    _source.GetBoxGeometry(_colliders.Count), CollisionFilter.Default, Material.Default);
                if (!collider.IsCreated)
                    return false;

                PhysicsMass mass = PhysicsMass.CreateDynamic(
                    collider.Value.MassProperties, math.max(_source.PhysicsBody.Mass, 0.001f));
                mass.InverseInertia.x = 0f;
                mass.InverseInertia.y = 0f;
                _colliders.Add(collider);
                _masses.Add(mass);
            }

            return true;
        }

        private void ResetRuntime()
        {
            DestroyDebris();
            DisposeVariantResources();
            _runtimeReady = false;
            _generation = -1;
        }

        private void DisposeVariantResources()
        {
            for (int i = 0; i < _colliders.Count; i++)
            {
                if (_colliders[i].IsCreated)
                    _colliders[i].Dispose();
            }
            _colliders.Clear();
            _masses.Clear();
        }

        private void DestroyDebris()
        {
            if (!World.IsCreated || _debrisQuery.IsEmptyIgnoreFilter)
                return;

            CompleteDependency();
            EntityManager.DestroyEntity(_debrisQuery);
        }
    }
}
