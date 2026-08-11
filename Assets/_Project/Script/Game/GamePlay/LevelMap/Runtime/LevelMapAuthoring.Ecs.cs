using Crusher;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using Collider = Unity.Physics.Collider;
using PhysicsMaterial = Unity.Physics.Material;
using RenderMaterial = UnityEngine.Material;

public sealed partial class LevelMapAuthoring
{
    private bool EnsureReleasedBlockResources()
    {
        if (_releasedBlockTypes.Count == 0)
            return false;

        EnsureRenderFrameResources();
        return true;
    }

    private void BuildAuthoredMeshLibrary()
    {
        // The map render uses the authored block mesh. Keep every face here so the generated
        // chunk preserves the block's depth instead of becoming a front-only card.
        DisposeAuthoredMeshLibrary();
        int vertexCount = 0;
        int indexCount = 0;
        for (int i = 0; i < _releasedBlockTypes.Count; i++)
        {
            Mesh mesh = _releasedBlockTypes[i].Mesh;
            vertexCount += mesh.vertexCount;
            indexCount += mesh.triangles.Length;
        }

        _authoredMeshVertices = new NativeArray<Vector3>(vertexCount, Allocator.Persistent);
        _authoredMeshUvs = new NativeArray<Vector2>(vertexCount, Allocator.Persistent);
        _authoredMeshIndices = new NativeArray<int>(indexCount, Allocator.Persistent);
        _authoredMeshRanges = new NativeArray<AuthoredMeshRange>(_releasedBlockTypes.Count, Allocator.Persistent);

        int vertexStart = 0;
        int indexStart = 0;
        for (int i = 0; i < _releasedBlockTypes.Count; i++)
        {
            Mesh mesh = _releasedBlockTypes[i].Mesh;
            Vector3[] vertices = mesh.vertices;
            float sourceDepth = mesh.bounds.size.z;
            float depthScale = sourceDepth > 0.0001f
                ? _chunkColliderDepth / (Mathf.Min(_cellSize.x, _cellSize.y) * sourceDepth)
                : 1f;
            Vector2[] uvs = mesh.uv;
            int[] indices = mesh.triangles;
            _authoredMeshRanges[i] = new AuthoredMeshRange
            {
                VertexStart = vertexStart,
                VertexCount = vertices.Length,
                IndexStart = indexStart,
                IndexCount = indices.Length
            };
            for (int vertex = 0; vertex < vertices.Length; vertex++)
            {
                vertices[vertex].z *= depthScale;
                _authoredMeshVertices[vertexStart + vertex] = vertices[vertex];
                _authoredMeshUvs[vertexStart + vertex] = uvs.Length == vertices.Length ? uvs[vertex] : Vector2.zero;
            }
            int writeIndex = indexStart;
            for (int index = 0; index < indices.Length; index += 3)
            {
                _authoredMeshIndices[writeIndex++] = indices[index];
                _authoredMeshIndices[writeIndex++] = indices[index + 1];
                _authoredMeshIndices[writeIndex++] = indices[index + 2];
            }
            vertexStart += vertices.Length;
            indexStart += indices.Length;
        }
    }

    private void DisposeAuthoredMeshLibrary()
    {
        if (_authoredMeshVertices.IsCreated)
            _authoredMeshVertices.Dispose();
        if (_authoredMeshUvs.IsCreated)
            _authoredMeshUvs.Dispose();
        if (_authoredMeshIndices.IsCreated)
            _authoredMeshIndices.Dispose();
        if (_authoredMeshRanges.IsCreated)
            _authoredMeshRanges.Dispose();
    }

    private int GetOrCreateReleasedBlockType(TypeBlockMap blockMap, Mesh fragmentMesh)
    {
        float worldX = _runtimeParent.TransformVector(Vector3.right * _cellSize.x).magnitude;
        float worldY = _runtimeParent.TransformVector(Vector3.up * _cellSize.y).magnitude;
        float worldZ = _runtimeParent.TransformVector(Vector3.forward * _chunkColliderDepth).magnitude;
        float shortestWorldSide = Mathf.Min(worldX, worldY);
        Vector3 renderScale = new(worldX / shortestWorldSide, worldY / shortestWorldSide,
            worldZ / (shortestWorldSide * fragmentMesh.bounds.size.z));
        return GetOrCreateReleasedBlockType(blockMap.ReleasedPrefab, shortestWorldSide, renderScale,
            blockMap.BlockType, blockMap, fragmentMesh);
    }

    private int GetOrCreateReleasedBlockType(LevelDecoration decoration)
    {
        if (!decoration.ReleasesCollectible)
            return -1;

        return GetOrCreateReleasedBlockType(decoration.ReleasedPrefab, decoration.ReleasedScale, Vector3.one,
            decoration.BlockType, decoration);
    }

    private int GetOrCreateReleasedBlockType(GameObject prefab, float scale, Vector3 renderScale,
        TypeBlock collectibleType, Component source, Mesh authoredMesh = null)
    {
        if (prefab == null || scale <= 0f || collectibleType == TypeBlock.None)
        {
            Debug.LogError($"'{source.name}' needs a collectible type, released prefab and positive scale.", source);
            return -1;
        }

        MeshFilter meshFilter = prefab.GetComponentInChildren<MeshFilter>();
        Renderer meshRenderer = prefab.GetComponentInChildren<Renderer>();
        Mesh sourceMesh = authoredMesh != null ? authoredMesh : meshFilter != null ? meshFilter.sharedMesh : null;
        if (sourceMesh == null || meshRenderer == null)
        {
            Debug.LogError($"Released prefab '{prefab.name}' needs a mesh filter and renderer.", prefab);
            return -1;
        }

        for (int i = 0; i < _releasedBlockTypes.Count; i++)
        {
            ReleasedBlockRuntimeType existing = _releasedBlockTypes[i];
            if (existing.Prefab == prefab && existing.Mesh == sourceMesh && Mathf.Approximately(existing.Scale, scale) &&
                (existing.RenderScale - renderScale).sqrMagnitude <= 0.000001f &&
                existing.CollectibleType == collectibleType)
                return i;
        }

        RenderMaterial sourceMaterial = meshRenderer.sharedMaterial != null
            ? meshRenderer.sharedMaterial
            : _chunkMaterial;
        _releasedBlockPropertyBlock ??= new MaterialPropertyBlock();
        int batchCapacity = Mathf.CeilToInt(_maxReleasedPhysicsBlocks / (float)MaxInstancesPerBatch);
        Bounds bounds = sourceMesh.bounds;
        Vector3 scaledSize = Vector3.Scale(bounds.size, new Vector3(
            Mathf.Abs(renderScale.x), Mathf.Abs(renderScale.y), Mathf.Abs(renderScale.z)));
        Vector3 scaledCenter = Vector3.Scale(bounds.center, renderScale);
        float radius = Mathf.Min(scaledSize.x, scaledSize.y) * 0.48f;
        BlobAssetReference<Collider> collider;
        bool useBoxCollider = source is TypeBlockMap or BreakableObstacle;
        if (useBoxCollider)
        {
            Vector3 colliderSize = Vector3.Scale(scaledSize, new Vector3(0.78f, 0.78f, 0.9f));
            collider = Unity.Physics.BoxCollider.Create(new BoxGeometry
            {
                Center = new float3(scaledCenter.x, scaledCenter.y, scaledCenter.z),
                Size = new float3(colliderSize.x, colliderSize.y, colliderSize.z),
                Orientation = quaternion.identity,
                BevelRadius = Mathf.Min(colliderSize.x, Mathf.Min(colliderSize.y, colliderSize.z)) * 0.2f
            }, CollisionFilter.Default, CreatePhysicsMaterial());
        }
        else
        {
            collider = Unity.Physics.SphereCollider.Create(new SphereGeometry
            {
                Center = new float3(scaledCenter.x, scaledCenter.y, scaledCenter.z),
                Radius = radius
            }, CollisionFilter.Default, CreatePhysicsMaterial());
        }
#if UNITY_EDITOR
        Debug.Assert(!useBoxCollider || collider.Value.Type == ColliderType.Box,
            $"Released block '{source.name}' must use a Box collider.", source);
#endif
        RenderMaterial material = new RenderMaterial(sourceMaterial) { enableInstancing = true };
        ReleasedBlockRuntimeType runtimeType = new ReleasedBlockRuntimeType
        {
            Prefab = prefab,
            Mesh = sourceMesh,
            Material = material,
            Collider = collider,
            Scale = scale,
            RenderScale = renderScale,
            Radius = radius * scale,
            CollectibleType = collectibleType,
            BatchMatrices = new Matrix4x4[batchCapacity][],
            BatchColors = new Vector4[batchCapacity][],
            BatchCounts = new int[batchCapacity]
        };
        for (int i = 0; i < batchCapacity; i++)
        {
            runtimeType.BatchMatrices[i] = new Matrix4x4[MaxInstancesPerBatch];
            runtimeType.BatchColors[i] = new Vector4[MaxInstancesPerBatch];
        }
        _releasedBlockTypes.Add(runtimeType);

        EnsureRenderFrameResources();
        return _releasedBlockTypes.Count - 1;
    }

    private bool EnsureEcsReady()
    {
        // Cleanup can run after Unity has already torn down the default ECS world.
        // Never initialize a new world from here: doing so during scene unload creates
        // a new GameObject after the scene has started closing.
        World world = _ecsWorld != null && _ecsWorld.IsCreated
            ? _ecsWorld
            : World.DefaultGameObjectInjectionWorld;
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
            step.CollisionTolerance = Mathf.Max(step.CollisionTolerance, GetWorldCellSize() * 0.1f);
            _entityManager.CreateSingleton(step, "Gameplay Physics Step");
        }
        else
        {
            Entity stepEntity = query.GetSingletonEntity();
            PhysicsStep step = _entityManager.GetComponentData<PhysicsStep>(stepEntity);
            float collisionTolerance = Mathf.Max(step.CollisionTolerance, GetWorldCellSize() * 0.1f);
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
