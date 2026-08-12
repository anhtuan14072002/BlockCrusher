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
        private readonly List<BlobAssetReference<Collider>> _contactColliders = new(16);
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

            int spawnCount = math.min(_source.ReadyCount, _source.MaxSpawnsPerFrame);
            if (spawnCount == 0)
                return;

            int recycleCount = math.max(0,
                _debrisQuery.CalculateEntityCount() + spawnCount - _source.MaxActiveBlocks);
            if (recycleCount > 0)
            {
                using NativeArray<Entity> debris = _debrisQuery.ToEntityArray(Allocator.Temp);
                recycleCount = math.min(recycleCount, debris.Length);
                for (int i = 0; i < recycleCount; i++)
                    EntityManager.DestroyEntity(debris[i]);
            }

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
                EntityManager.SetComponentData(entity,
                    new PhysicsCollider { Value = _colliders[request.VariantIndex] });
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
                    PhysicsStepStartPosition = new float3(
                        request.Position.x, request.Position.y, request.Position.z),
                    LockedZ = request.Position.z,
                    MaxPlanarSpeed = _source.MaxPlanarSpeed,
                    BaseScale = request.Scale,
                    OwnerId = request.OwnerId,
                    VariantIndex = request.VariantIndex
                });
                EntityManager.SetComponentEnabled<CutDebrisSuctionTransit>(entity, false);
                EntityManager.SetComponentData(entity, new CutDebrisPendingActivation
                {
                    ContactCollider = _contactColliders[request.VariantIndex],
                    InitialVelocity = new float3(
                        request.InitialVelocity.x, request.InitialVelocity.y, request.InitialVelocity.z),
                    InitialAngularVelocity = request.InitialAngularVelocity
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
                typeof(LocalTransform), typeof(PhysicsCollider), typeof(PhysicsMass), typeof(PhysicsVelocity),
                typeof(PhysicsDamping), typeof(PhysicsGravityFactor), typeof(Simulate),
                typeof(PhysicsWorldIndex), typeof(CutDebrisComponent), typeof(CutDebrisSuctionTransit),
                typeof(CutDebrisPendingActivation));
            _runtimeReady = true;
            _generation = _source.Generation;
            return true;
        }

        private bool EnsureVariantResources()
        {
            CollisionFilter collisionFilter = new()
            {
                BelongsTo = DeviceMeshCollider.CutDebrisCategory,
                CollidesWith = uint.MaxValue & ~DeviceMeshCollider.SawCategory
            };
            CollisionFilter contactFilter = collisionFilter;
            contactFilter.CollidesWith = uint.MaxValue;
            Material material = Material.Default;
            material.Friction = 0.12f;
            material.Restitution = 0.05f;
            while (_colliders.Count < _source.VariantCount)
            {
                var geometry = _source.GetBoxGeometry(_colliders.Count);
                BlobAssetReference<Collider> collider = BoxCollider.Create(
                    geometry, collisionFilter, material);
                BlobAssetReference<Collider> contactCollider = BoxCollider.Create(
                    geometry, contactFilter, material);
                if (!collider.IsCreated || !contactCollider.IsCreated)
                {
                    if (collider.IsCreated)
                        collider.Dispose();
                    if (contactCollider.IsCreated)
                        contactCollider.Dispose();
                    return false;
                }
#if UNITY_EDITOR
                UnityEngine.Debug.Assert(collider.Value.Type == ColliderType.Box &&
                                         (collider.Value.GetCollisionFilter().CollidesWith &
                                          DeviceMeshCollider.SawCategory) == 0 &&
                                         (collider.Value.GetCollisionFilter().CollidesWith &
                                          DeviceMeshCollider.CutDebrisCategory) != 0,
                    "Cut debris must keep Box-to-box collision without saw depenetration.");
                UnityEngine.Debug.Assert(contactCollider.Value.Type == ColliderType.Box &&
                                         (contactCollider.Value.GetCollisionFilter().CollidesWith &
                                          DeviceMeshCollider.SawCategory) != 0,
                    "Clear cut debris must restore real saw contact.");
#endif

                PhysicsMass mass = PhysicsMass.CreateDynamic(
                    collider.Value.MassProperties, math.max(_source.PhysicsBody.Mass, 0.001f));
                mass.InverseInertia.x = 0f;
                mass.InverseInertia.y = 0f;
                _colliders.Add(collider);
                _contactColliders.Add(contactCollider);
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
            for (int i = 0; i < _contactColliders.Count; i++)
            {
                if (_contactColliders[i].IsCreated)
                    _contactColliders[i].Dispose();
            }
            _colliders.Clear();
            _contactColliders.Clear();
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
