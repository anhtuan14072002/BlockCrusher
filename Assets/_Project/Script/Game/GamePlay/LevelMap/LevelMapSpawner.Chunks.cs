using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using Collider = Unity.Physics.Collider;
using PhysicsMaterial = Unity.Physics.Material;

public sealed partial class LevelMapSpawner
{
    private void CreateChunks()
    {
        _chunkColumns = Mathf.CeilToInt(_gridWidth / (float)_chunkSize);
        int chunkRows = Mathf.CeilToInt(_gridHeight / (float)_chunkSize);
        int chunkCount = _chunkColumns * chunkRows;
        _chunkDirty = new byte[chunkCount];
        for (int chunkY = 0; chunkY < chunkRows; chunkY++)
        {
            for (int chunkX = 0; chunkX < _chunkColumns; chunkX++)
            {
                int startX = chunkX * _chunkSize;
                int startY = chunkY * _chunkSize;
                int width = Mathf.Min(_chunkSize, _gridWidth - startX);
                int height = Mathf.Min(_chunkSize, _gridHeight - startY);
                GameObject chunkObject = new GameObject("LevelChunk_" + chunkX + "_" + chunkY);
                chunkObject.transform.SetParent(_runtimeParent, false);
                MeshFilter meshFilter = chunkObject.AddComponent<MeshFilter>();
                MeshRenderer meshRenderer = chunkObject.AddComponent<MeshRenderer>();
                LevelMapChunk chunk = chunkObject.AddComponent<LevelMapChunk>();
                Mesh mesh = new Mesh { name = "LevelChunk_Mesh_" + chunkX + "_" + chunkY };
                mesh.MarkDynamic();
                meshFilter.sharedMesh = mesh;
                meshRenderer.sharedMaterial = _chunkMaterial;
                chunk.Initialize(this);
                _chunks.Add(CreateChunkRuntime(mesh, meshRenderer, startX, startY, width, height));
            }
        }
        _scheduledChunkRebuilds.Clear();
        for (int i = 0; i < _chunks.Count; i++)
            _scheduledChunkRebuilds.Add(i);
        ScheduleAndApplyChunkRebuilds();
    }
    private ChunkRuntime CreateChunkRuntime(Mesh mesh, MeshRenderer renderer, int startX, int startY, int width,
        int height)
    {
        int maxCells = width * height;
        int maxVerticesPerBlock = 4;
        int maxIndicesPerBlock = 6;
        for (int i = 0; i < _authoredMeshRanges.Length; i++)
        {
            AuthoredMeshRange range = _authoredMeshRanges[i];
            maxVerticesPerBlock = Mathf.Max(maxVerticesPerBlock, range.VertexCount);
            maxIndicesPerBlock = Mathf.Max(maxIndicesPerBlock, range.IndexCount);
        }
        int vertexCapacity = maxCells * maxVerticesPerBlock;
        int indexCapacity = maxCells * maxIndicesPerBlock;
        return new ChunkRuntime
        {
            Mesh = mesh,
            Renderer = renderer,
            StartX = startX,
            StartY = startY,
            Width = width,
            Height = height,
            Visited = new NativeArray<byte>(maxCells, Allocator.Persistent),
            Vertices = new NativeList<Vector3>(vertexCapacity, Allocator.Persistent),
            Colors = new NativeList<Color32>(vertexCapacity, Allocator.Persistent),
            Uvs = new NativeList<Vector2>(vertexCapacity, Allocator.Persistent),
            Indices = new NativeList<int>(indexCapacity, Allocator.Persistent),
            ColliderVisited = new NativeArray<byte>(maxCells, Allocator.Persistent),
            ColliderVertices = new NativeList<Vector3>(maxCells * 24, Allocator.Persistent),
            ColliderColors = new NativeList<Color32>(maxCells * 24, Allocator.Persistent),
            ColliderUvs = new NativeList<Vector2>(maxCells * 24, Allocator.Persistent),
            ColliderIndices = new NativeList<int>(maxCells * 36, Allocator.Persistent)
        };
    }
    private void ScheduleAndApplyChunkRebuilds()
    {
        if (_scheduledChunkRebuilds.Count == 0)
            return;
        _scheduledChunkHandles.Clear();
        for (int i = 0; i < _scheduledChunkRebuilds.Count; i++)
        {
            int chunkIndex = _scheduledChunkRebuilds[i];
            if ((uint)chunkIndex >= (uint)_chunks.Count)
                continue;
            ChunkRuntime chunk = _chunks[chunkIndex];
            chunk.Vertices.Clear();
            chunk.Colors.Clear();
            chunk.Uvs.Clear();
            chunk.Indices.Clear();
            chunk.ColliderVertices.Clear();
            chunk.ColliderColors.Clear();
            chunk.ColliderUvs.Clear();
            chunk.ColliderIndices.Clear();
            BuildChunkMeshJob meshJob = new BuildChunkMeshJob
            {
                CellColors = _cellColors,
                CellSolid = _cellSolid,
                CellReleasedTypes = _cellReleasedTypes,
                AuthoredMeshVertices = _authoredMeshVertices,
                AuthoredMeshUvs = _authoredMeshUvs,
                AuthoredMeshIndices = _authoredMeshIndices,
                AuthoredMeshRanges = _authoredMeshRanges,
                Visited = chunk.Visited,
                Vertices = chunk.Vertices,
                Colors = chunk.Colors,
                Uvs = chunk.Uvs,
                Indices = chunk.Indices,
                GridWidth = _gridWidth,
                StartX = chunk.StartX,
                StartY = chunk.StartY,
                ChunkWidth = chunk.Width,
                ChunkHeight = chunk.Height,
                CellSize = _cellSize,
                Offset = _offset,
                ExtrudeMergedQuads = 0,
                MergeAnySolid = 0,
                UseAuthoredMeshes = 1,
                DetailDepth = _chunkColliderDepth
            };
            _scheduledChunkHandles.Add(meshJob.Schedule());
            BuildChunkMeshJob colliderJob = new BuildChunkMeshJob
            {
                CellColors = _cellColors,
                CellSolid = _cellSolid,
                CellReleasedTypes = _cellReleasedTypes,
                AuthoredMeshVertices = _authoredMeshVertices,
                AuthoredMeshUvs = _authoredMeshUvs,
                AuthoredMeshIndices = _authoredMeshIndices,
                AuthoredMeshRanges = _authoredMeshRanges,
                Visited = chunk.ColliderVisited,
                Vertices = chunk.ColliderVertices,
                Colors = chunk.ColliderColors,
                Uvs = chunk.ColliderUvs,
                Indices = chunk.ColliderIndices,
                GridWidth = _gridWidth,
                StartX = chunk.StartX,
                StartY = chunk.StartY,
                ChunkWidth = chunk.Width,
                ChunkHeight = chunk.Height,
                CellSize = _cellSize,
                Offset = _offset,
                ExtrudeMergedQuads = 1,
                MergeAnySolid = 1,
                UseAuthoredMeshes = 0,
                DetailDepth = _chunkColliderDepth
            };
            _scheduledChunkHandles.Add(colliderJob.Schedule());
        }
        JobHandle combinedHandle = default;
        for (int i = 0; i < _scheduledChunkHandles.Count; i++)
            combinedHandle = JobHandle.CombineDependencies(combinedHandle, _scheduledChunkHandles[i]);
        combinedHandle.Complete();
        for (int i = 0; i < _scheduledChunkRebuilds.Count; i++)
        {
            int chunkIndex = _scheduledChunkRebuilds[i];
            if ((uint)chunkIndex >= (uint)_chunks.Count)
                continue;
            ChunkRuntime chunk = _chunks[chunkIndex];
            ApplyChunkMesh(ref chunk);
            ApplyChunkDecorations(ref chunk);
            _chunks[chunkIndex] = chunk;
        }
    }
    private void ApplyChunkMesh(ref ChunkRuntime chunk)
    {
        Mesh mesh = chunk.Mesh;
        mesh.Clear();
        if (chunk.Vertices.Length > 0)
        {
            mesh.indexFormat = chunk.Vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(chunk.Vertices.AsArray());
            mesh.SetColors(chunk.Colors.AsArray());
            mesh.SetUVs(0, chunk.Uvs.AsArray());
            mesh.SetIndices(chunk.Indices.AsArray(), MeshTopology.Triangles, 0);
            mesh.RecalculateBounds();
            chunk.Renderer.enabled = true;
            ApplyChunkPhysicsCollider(ref chunk);
        }
        else
        {
            chunk.Renderer.enabled = false;
            DestroyChunkPhysicsCollider(ref chunk);
        }
    }
    private void ApplyChunkPhysicsCollider(ref ChunkRuntime chunk)
    {
        if (!EnsureEcsReady())
            return;
        if (chunk.ColliderVertices.Length == 0 || chunk.ColliderIndices.Length == 0)
        {
            DestroyChunkPhysicsCollider(ref chunk);
            return;
        }
        int triangleCount = chunk.ColliderIndices.Length / 3;
        NativeArray<float3> vertices = new NativeArray<float3>(chunk.ColliderVertices.Length, Allocator.Temp);
        NativeArray<int3> triangles = new NativeArray<int3>(triangleCount, Allocator.Temp);
        for (int i = 0; i < chunk.ColliderVertices.Length; i++)
        {
            Vector3 world = _runtimeParent.TransformPoint(chunk.ColliderVertices[i]);
            vertices[i] = new float3(world.x, world.y, world.z);
        }
        for (int i = 0; i < triangleCount; i++)
        {
            int index = i * 3;
            triangles[i] = new int3(chunk.ColliderIndices[index], chunk.ColliderIndices[index + 1],
                chunk.ColliderIndices[index + 2]);
        }
        PhysicsMaterial physicsMaterial = CreatePhysicsMaterial();
        BlobAssetReference<Collider> collider = Unity.Physics.MeshCollider.Create(vertices, triangles,
            CollisionFilter.Default, physicsMaterial);
        vertices.Dispose();
        triangles.Dispose();
        BlobAssetReference<Collider> previousCollider = chunk.PhysicsCollider;
        chunk.PhysicsCollider = collider;
        if (chunk.PhysicsEntity == Entity.Null || !_entityManager.Exists(chunk.PhysicsEntity))
        {
            chunk.PhysicsEntity = _entityManager.CreateEntity(typeof(LocalTransform), typeof(PhysicsCollider));
            _entityManager.SetComponentData(chunk.PhysicsEntity,
                LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));
            _entityManager.AddSharedComponent(chunk.PhysicsEntity, new PhysicsWorldIndex(0));
        }
        _entityManager.SetComponentData(chunk.PhysicsEntity, new PhysicsCollider { Value = chunk.PhysicsCollider });
        if (previousCollider.IsCreated)
            previousCollider.Dispose();
    }
    private void DestroyChunkPhysicsCollider(ref ChunkRuntime chunk)
    {
        if (EnsureEcsReady() && chunk.PhysicsEntity != Entity.Null && _entityManager.Exists(chunk.PhysicsEntity))
            _entityManager.DestroyEntity(chunk.PhysicsEntity);
        chunk.PhysicsEntity = Entity.Null;
        if (chunk.PhysicsCollider.IsCreated)
        {
            chunk.PhysicsCollider.Dispose();
            chunk.PhysicsCollider = default;
        }
    }
    private void DisposeChunks()
    {
        for (int i = 0; i < _chunks.Count; i++)
        {
            ChunkRuntime chunk = _chunks[i];
            if (chunk.Visited.IsCreated)
                chunk.Visited.Dispose();
            if (chunk.Vertices.IsCreated)
                chunk.Vertices.Dispose();
            if (chunk.Colors.IsCreated)
                chunk.Colors.Dispose();
            if (chunk.Uvs.IsCreated)
                chunk.Uvs.Dispose();
            if (chunk.Indices.IsCreated)
                chunk.Indices.Dispose();
            if (chunk.ColliderVisited.IsCreated)
                chunk.ColliderVisited.Dispose();
            if (chunk.ColliderVertices.IsCreated)
                chunk.ColliderVertices.Dispose();
            if (chunk.ColliderColors.IsCreated)
                chunk.ColliderColors.Dispose();
            if (chunk.ColliderUvs.IsCreated)
                chunk.ColliderUvs.Dispose();
            if (chunk.ColliderIndices.IsCreated)
                chunk.ColliderIndices.Dispose();
            DestroyChunkPhysicsCollider(ref chunk);
            if (chunk.Mesh != null)
                DestroyUnityObject(chunk.Mesh);
        }
        _chunks.Clear();
    }
    private Vector3 GetCellLocalPosition(int x, int y)
    {
        return _offset + new Vector3(x * _cellSize, y * _cellSize, 0f);
    }
}
