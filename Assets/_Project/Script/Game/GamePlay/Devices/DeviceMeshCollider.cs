using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using Collider = Unity.Physics.Collider;
using PhysicsMaterial = Unity.Physics.Material;

namespace Crusher
{
    internal static class DeviceMeshCollider
    {
        public static bool TryCreate(Mesh mesh, Vector3 scale, out BlobAssetReference<Collider> collider)
        {
            collider = default;
            if (mesh == null || !mesh.isReadable)
                return false;

            Vector3[] sourceVertices = mesh.vertices;
            int[] sourceTriangles = mesh.triangles;
            using NativeList<float3> vertices = new(sourceVertices.Length, Allocator.Temp);
            using NativeList<int3> triangles = new(sourceTriangles.Length / 3, Allocator.Temp);
            for (int i = 0; i < sourceVertices.Length; i++)
            {
                Vector3 vertex = Vector3.Scale(sourceVertices[i], scale);
                vertices.Add(new float3(vertex.x, vertex.y, vertex.z));
            }

            for (int i = 0; i < sourceTriangles.Length; i += 3)
                triangles.Add(new int3(sourceTriangles[i], sourceTriangles[i + 1], sourceTriangles[i + 2]));

            collider = Unity.Physics.MeshCollider.Create(
                vertices.AsArray(), triangles.AsArray(), CollisionFilter.Default, PhysicsMaterial.Default);
            return collider.IsCreated;
        }
    }
}
