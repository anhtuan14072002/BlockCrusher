using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using Collider = Unity.Physics.Collider;
using PhysicsMaterial = Unity.Physics.Material;
using RenderMaterial = UnityEngine.Material;

public sealed partial class LevelMapSpawner
{
    private bool EnsureReleasedBlockResources()
    {
        if (_releasedBlockTypes.Count == 0)
            return false;

        EnsureRenderFrameResources();
        return true;
    }

    private int GetOrCreateReleasedBlockType(LevelBlock block)
    {
        return GetOrCreateReleasedBlockType(block.ReleasedPrefab, block.ReleasedScale, block.CollectibleId, block);
    }

    private int GetOrCreateReleasedBlockType(LevelDecoration decoration)
    {
        if (!decoration.ReleasesCollectible)
            return -1;

        return GetOrCreateReleasedBlockType(decoration.ReleasedPrefab, decoration.ReleasedScale,
            decoration.CollectibleId, decoration);
    }

    private int GetOrCreateReleasedBlockType(GameObject prefab, float scale, string collectibleName,
        Component source)
    {
        if (prefab == null || scale <= 0f || string.IsNullOrWhiteSpace(collectibleName) ||
            collectibleName.Length > 60)
        {
            Debug.LogError($"'{source.name}' needs a collectible id up to 60 characters, released prefab and positive scale.", source);
            return -1;
        }
        FixedString64Bytes collectibleId = collectibleName;

        for (int i = 0; i < _releasedBlockTypes.Count; i++)
        {
            ReleasedBlockRuntimeType existing = _releasedBlockTypes[i];
            if (existing.Prefab == prefab && Mathf.Approximately(existing.Scale, scale) &&
                existing.CollectibleId.Equals(collectibleId))
                return existing.FirstVariantIndex;
        }

        MeshFilter meshFilter = prefab.GetComponentInChildren<MeshFilter>();
        Renderer meshRenderer = prefab.GetComponentInChildren<Renderer>();
        if (meshFilter == null || meshFilter.sharedMesh == null || meshRenderer == null)
        {
            Debug.LogError($"Released prefab '{prefab.name}' needs a mesh filter and renderer.", prefab);
            return -1;
        }

        Mesh sourceMesh = meshFilter.sharedMesh;
        RenderMaterial sourceMaterial = meshRenderer.sharedMaterial != null
            ? meshRenderer.sharedMaterial
            : _chunkMaterial;
        _releasedBlockPropertyBlock ??= new MaterialPropertyBlock();
        int firstVariantIndex = _releasedBlockTypes.Count;
        int batchCapacity = Mathf.CeilToInt(_maxReleasedPhysicsBlocks / (float)MaxInstancesPerBatch);
        for (int variant = 0; variant < ReleasedMeshVariantCount; variant++)
        {
            Mesh mesh = CreateReleasedVariantMesh(sourceMesh, variant);
            Bounds bounds = mesh.bounds;
            float radius = Mathf.Min(bounds.size.x, bounds.size.y) * 0.48f;
            BlobAssetReference<Collider> collider = Unity.Physics.SphereCollider.Create(new SphereGeometry
            {
                Center = bounds.center,
                Radius = radius
            }, CollisionFilter.Default, CreatePhysicsMaterial());
            RenderMaterial material = new RenderMaterial(sourceMaterial) { enableInstancing = true };
            ReleasedBlockRuntimeType runtimeType = new ReleasedBlockRuntimeType
            {
                Prefab = prefab,
                Mesh = mesh,
                Material = material,
                Collider = collider,
                Scale = scale,
                Radius = radius * scale,
                CollectibleId = collectibleId,
                BatchMatrices = new Matrix4x4[batchCapacity][],
                BatchColors = new Vector4[batchCapacity][],
                BatchCounts = new int[batchCapacity],
                FirstVariantIndex = (ushort)firstVariantIndex,
                VariantCount = ReleasedMeshVariantCount,
                OwnsMesh = mesh != sourceMesh
            };
            for (int i = 0; i < batchCapacity; i++)
            {
                runtimeType.BatchMatrices[i] = new Matrix4x4[MaxInstancesPerBatch];
                runtimeType.BatchColors[i] = new Vector4[MaxInstancesPerBatch];
            }
            _releasedBlockTypes.Add(runtimeType);
        }

        EnsureRenderFrameResources();
        return firstVariantIndex;
    }

    private Mesh CreateReleasedVariantMesh(Mesh source, int variant)
    {
        if (!source.isReadable)
            return source;

        Vector3[] vertices = source.vertices;
        Bounds bounds = source.bounds;
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        int cellX = variant & 1;
        int cellY = (variant >> 1) & 1;
        Vector2 bottomLeftOffset = LevelMapMeshBuilder.GetGridCornerOffset(
            cellX, cellY, _terrainCellIrregularity);
        Vector2 topLeftOffset = LevelMapMeshBuilder.GetGridCornerOffset(
            cellX, cellY + 1, _terrainCellIrregularity);
        Vector2 topRightOffset = LevelMapMeshBuilder.GetGridCornerOffset(
            cellX + 1, cellY + 1, _terrainCellIrregularity);
        Vector2 bottomRightOffset = LevelMapMeshBuilder.GetGridCornerOffset(
            cellX + 1, cellY, _terrainCellIrregularity);
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 point = vertices[i] - center;
            float u = extents.x > 0.0001f ? Mathf.InverseLerp(-extents.x, extents.x, point.x) : 0.5f;
            float v = extents.y > 0.0001f ? Mathf.InverseLerp(-extents.y, extents.y, point.y) : 0.5f;
            Vector2 bottomOffset = Vector2.Lerp(bottomLeftOffset, bottomRightOffset, u);
            Vector2 topOffset = Vector2.Lerp(topLeftOffset, topRightOffset, u);
            Vector2 offset = Vector2.Lerp(bottomOffset, topOffset, v);
            point.x += offset.x * extents.x * 2f;
            point.y += offset.y * extents.y * 2f;
            vertices[i] = center + point;
        }

        Mesh mesh = new Mesh { name = $"{source.name}_Debris{variant}" };
        mesh.indexFormat = source.indexFormat;
        mesh.vertices = vertices;
        mesh.triangles = source.triangles;
        mesh.uv = source.uv;
        mesh.colors32 = source.colors32;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
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
