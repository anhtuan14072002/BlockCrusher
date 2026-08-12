using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using UnityEngine.Rendering;
using Collider = Unity.Physics.Collider;
using PhysicsMaterial = Unity.Physics.Material;

namespace Crusher
{
    internal static class DeviceMeshCollider
    {
        public const uint SawCategory = 1u << 31;
        public const uint CutDebrisCategory = 1u << 30;

        public static bool TryCreate(Mesh mesh, Vector3 scale,
            CollisionResponsePolicy collisionResponse, out BlobAssetReference<Collider> collider,
            int subMeshIndex = -1)
        {
            return TryCreate(mesh, scale, collisionResponse, CollisionFilter.Default,
                out collider, subMeshIndex);
        }

        public static bool TryCreate(Mesh mesh, Vector3 scale,
            CollisionResponsePolicy collisionResponse, CollisionFilter collisionFilter,
            out BlobAssetReference<Collider> collider, int subMeshIndex = -1)
        {
            collider = default;
            if (!TryGetGeometry(mesh, subMeshIndex, out _, out Vector3[] sourceVertices,
                    out int[] sourceTriangles))
                return false;

            using NativeList<float3> vertices = new(sourceVertices.Length, Allocator.Temp);
            using NativeList<int3> triangles = new(sourceTriangles.Length / 3, Allocator.Temp);
            for (int i = 0; i < sourceVertices.Length; i++)
            {
                Vector3 vertex = Vector3.Scale(sourceVertices[i], scale);
                vertices.Add(new float3(vertex.x, vertex.y, vertex.z));
            }

            for (int i = 0; i < sourceTriangles.Length; i += 3)
                triangles.Add(new int3(sourceTriangles[i], sourceTriangles[i + 1], sourceTriangles[i + 2]));

            PhysicsMaterial material = PhysicsMaterial.Default;
            material.CollisionResponse = collisionResponse;
            collider = Unity.Physics.MeshCollider.Create(
                vertices.AsArray(), triangles.AsArray(), collisionFilter, material);
            return collider.IsCreated;
        }

        public static bool TryGetGeometry(Mesh mesh, int subMeshIndex, out Bounds bounds,
            out Vector3[] vertices, out int[] triangles)
        {
            bounds = default;
            vertices = null;
            triangles = null;
            if (mesh == null)
                return false;

            using Mesh.MeshDataArray meshDataArray = Mesh.AcquireReadOnlyMeshData(mesh);
            Mesh.MeshData meshData = meshDataArray[0];
            if (subMeshIndex >= meshData.subMeshCount)
                return false;

            using NativeArray<Vector3> vertexData =
                new(meshData.vertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            meshData.GetVertices(vertexData);
            vertices = vertexData.ToArray();

            if (subMeshIndex >= 0)
            {
                SubMeshDescriptor subMesh = meshData.GetSubMesh(subMeshIndex);
                using NativeArray<int> indexData =
                    new(subMesh.indexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                meshData.GetIndices(indexData, subMeshIndex, true);
                triangles = indexData.ToArray();
                bounds = subMesh.bounds;
            }
            else
            {
                int indexCount = 0;
                for (int i = 0; i < meshData.subMeshCount; i++)
                    indexCount += meshData.GetSubMesh(i).indexCount;

                triangles = new int[indexCount];
                int offset = 0;
                for (int i = 0; i < meshData.subMeshCount; i++)
                {
                    SubMeshDescriptor subMesh = meshData.GetSubMesh(i);
                    using NativeArray<int> indexData =
                        new(subMesh.indexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                    meshData.GetIndices(indexData, i, true);
                    for (int j = 0; j < indexData.Length; j++)
                        triangles[offset + j] = indexData[j];
                    offset += subMesh.indexCount;
                }

                bounds = mesh.bounds;
            }

            return triangles.Length >= 3;
        }
    }
}
