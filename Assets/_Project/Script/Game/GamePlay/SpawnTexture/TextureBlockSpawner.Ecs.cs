using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using Collider = Unity.Physics.Collider;
using PhysicsMaterial = Unity.Physics.Material;
using RenderMaterial = UnityEngine.Material;

public sealed partial class TextureBlockSpawner
{
    private bool EnsureReleasedBlockResources()
    {
        if (_releasedBlockCollider.IsCreated && _releasedBlockMesh != null && _releasedBlockMaterial != null)
        {
            EnsureRenderFrameResources();
            return true;
        }
        ReleasedBlockAuthoring authoring = ResolveReleasedBlockAuthoring();
        GameObject prefab = authoring != null ? authoring.ReleasedBlockPrefab : null;
        if (prefab == null)
            return false;
        MeshFilter meshFilter = prefab.GetComponentInChildren<MeshFilter>();
        Renderer meshRenderer = prefab.GetComponentInChildren<Renderer>();
        if (meshFilter == null || meshFilter.sharedMesh == null || meshRenderer == null)
            return false;
        _releasedBlockMesh = meshFilter.sharedMesh;
        _releasedBlockMaterial = new RenderMaterial(meshRenderer.sharedMaterial != null
            ? meshRenderer.sharedMaterial
            : _runtimeChunkMaterial)
        {
            enableInstancing = true
        };
        _releasedBlockPropertyBlock ??= new MaterialPropertyBlock();
        _releasedBlockScale = Mathf.Max(0.0001f, authoring.Scale);
        Vector3 size = Vector3.one;
        Vector3 center = Vector3.zero;
        UnityEngine.BoxCollider box = prefab.GetComponentInChildren<UnityEngine.BoxCollider>();
        if (box != null)
        {
            size = Vector3.Scale(box.size, box.transform.lossyScale);
            center = Vector3.Scale(box.center, box.transform.lossyScale);
        }
        else
        {
            Bounds bounds = _releasedBlockMesh.bounds;
            size = bounds.size;
            center = bounds.center;
        }
        PhysicsMaterial physicsMaterial = CreatePhysicsMaterial();
        float radius = Mathf.Min(size.x, size.y) * 0.48f;
        _releasedBlockCollider = Unity.Physics.SphereCollider.Create(new SphereGeometry
        {
            Center = new float3(center.x, center.y, center.z),
            Radius = radius
        }, CollisionFilter.Default, physicsMaterial);
        EnsureRenderFrameResources();
        return _releasedBlockCollider.IsCreated;
    }
    private ReleasedBlockAuthoring ResolveReleasedBlockAuthoring()
    {
        if (_releasedBlockAuthoring == null)
            _releasedBlockAuthoring = GetComponent<ReleasedBlockAuthoring>();
        return _releasedBlockAuthoring;
    }
    private bool EnsureEcsReady()
    {
        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated)
            return false;
        if (_ecsWorld != world || !_hasReleasedBlockQuery)
        {
            DisposeReleasedBlockWalls();
            DisposeEcsQuery();
            _ecsWorld = world;
            _entityManager = world.EntityManager;
            _releasedBlockArchetype = _entityManager.CreateArchetype(
                typeof(LocalTransform), typeof(PhysicsCollider), typeof(PhysicsMass), typeof(PhysicsVelocity),
                typeof(PhysicsDamping), typeof(PhysicsGravityFactor), typeof(Simulate),
                typeof(ReleasedBlockComponent), typeof(ReleasedBlockSolidConstraint), typeof(SuctionTransit),
                typeof(PhysicsWorldIndex));
            _releasedBlockQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<ReleasedBlockComponent>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadWrite<PhysicsVelocity>());
            _hasReleasedBlockQuery = true;
            EnsurePhysicsStep();
            EnsureReleasedBlockWalls();
            LevelObstacle.EnsurePhysicsEntities(_ecsWorld);
        }
        return true;
    }
    private void EnsurePhysicsStep()
    {
        EntityQuery query = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<PhysicsStep>());
        if (query.IsEmptyIgnoreFilter)
        {
            PhysicsStep step = PhysicsStep.Default;
            step.CollisionTolerance = Mathf.Max(step.CollisionTolerance, _cellSize * 0.1f);
            _entityManager.CreateSingleton(step, "Gameplay Physics Step");
        }
        else
        {
            Entity stepEntity = query.GetSingletonEntity();
            PhysicsStep step = _entityManager.GetComponentData<PhysicsStep>(stepEntity);
            float collisionTolerance = Mathf.Max(step.CollisionTolerance, _cellSize * 0.1f);
            if (!Mathf.Approximately(step.CollisionTolerance, collisionTolerance))
            {
                step.CollisionTolerance = collisionTolerance;
                _entityManager.SetComponentData(stepEntity, step);
            }
        }
        query.Dispose();
    }
    private void EnsureReleasedBlockWalls()
    {
        if (_releasedBlockWallEntities.Count > 0 || _releasedBlockWalls == null)
            return;
        PhysicsMaterial material = CreatePhysicsMaterial();
        for (int i = 0; i < _releasedBlockWalls.Length; i++)
        {
            Transform wall = _releasedBlockWalls[i];
            if (wall == null || !wall.gameObject.activeInHierarchy)
                continue;
            Vector3 scale = wall.lossyScale;
            Vector3 size = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            BlobAssetReference<Collider> collider = Unity.Physics.BoxCollider.Create(new BoxGeometry
            {
                Center = float3.zero,
                Size = new float3(size.x, size.y, size.z),
                Orientation = quaternion.identity,
                BevelRadius = 0f
            }, CollisionFilter.Default, material);
            Vector3 center = wall.position;
            Quaternion rotation = wall.rotation;
            Entity entity = _entityManager.CreateEntity(typeof(LocalTransform), typeof(PhysicsCollider));
            _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(
                new float3(center.x, center.y, center.z),
                new quaternion(rotation.x, rotation.y, rotation.z, rotation.w), 1f));
            _entityManager.SetComponentData(entity, new PhysicsCollider { Value = collider });
            _entityManager.AddSharedComponent(entity, new PhysicsWorldIndex(0));
            _releasedBlockWallColliders.Add(collider);
            _releasedBlockWallEntities.Add(entity);
        }
    }
}
