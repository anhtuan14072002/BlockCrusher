using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

namespace Crusher
{
    [UpdateInGroup(typeof(BeforePhysicsSystemGroup))]
    public partial class SawDeviceSystem : SystemBase
    {
        private sealed class CutMeshCache
        {
            public Mesh Mesh;
            public Bounds Bounds;
            public Vector3[] Vertices;
            public int[] Triangles;
        }

        private struct CutRequest
        {
            public Entity Entity;
            public SawComponent Saw;
            public float3 From;
            public float3 To;
            public quaternion Rotation;
        }

        private readonly Dictionary<Entity, CutMeshCache> _cutMeshes = new(1);
        private readonly List<CutRequest> _cutRequests = new(2);

        protected override void OnUpdate()
        {
            _cutRequests.Clear();
            foreach (var (sawReference, bodyReference, transformReference, entity) in
                     SystemAPI.Query<RefRO<SawComponent>, RefRO<DeviceBodyComponent>,
                         RefRO<LocalTransform>>().WithEntityAccess())
            {
                ref readonly SawComponent saw = ref sawReference.ValueRO;
                if (saw.Active == 0)
                    continue;

                float3 from = transformReference.ValueRO.Position;
                float3 to = bodyReference.ValueRO.TargetPosition;
                quaternion toRotation = bodyReference.ValueRO.TargetRotation;

                if (math.lengthsq(to.xy - from.xy) > 0.000001f)
                {
                    _cutRequests.Add(new CutRequest
                    {
                        Entity = entity,
                        Saw = saw,
                        From = from,
                        To = to,
                        Rotation = toRotation
                    });
                }
            }

            for (int i = 0; i < _cutRequests.Count; i++)
            {
                CutRequest request = _cutRequests[i];
                ReleaseMapAlongMovement(
                    request.Entity, in request.Saw, request.From, request.To, request.Rotation);
            }
        }

        private void ReleaseMapAlongMovement(Entity entity, in SawComponent saw,
            float3 from, float3 to, quaternion rotation)
        {
            Mesh mesh = saw.CutMesh.Value;
            if (mesh == null)
                return;

            if (!_cutMeshes.TryGetValue(entity, out CutMeshCache cache) || cache.Mesh != mesh)
            {
                if (!mesh.isReadable)
                    return;

                cache = new CutMeshCache
                {
                    Mesh = mesh,
                    Bounds = mesh.bounds,
                    Vertices = mesh.vertices,
                    Triangles = mesh.triangles
                };
                _cutMeshes[entity] = cache;
            }

            Vector3 fromPosition = new(from.x, from.y, from.z);
            Vector3 toPosition = new(to.x, to.y, to.z);
            float distance = Vector2.Distance(fromPosition, toPosition);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(saw.CutSweepStep, 0.01f)));
            Quaternion worldRotation = new(rotation.value.x, rotation.value.y, rotation.value.z, rotation.value.w);
            Vector3 scale = new(saw.Scale.x, saw.Scale.y, saw.Scale.z);
            Vector3 pushDirection = fromPosition - toPosition;
            pushDirection.z = 0f;
            if (pushDirection.sqrMagnitude > 0.000001f)
                pushDirection.Normalize();
            for (int i = 1; i <= steps; i++)
            {
                Vector3 position = Vector3.Lerp(fromPosition, toPosition, i / (float)steps);
                Matrix4x4 localToWorld = Matrix4x4.TRS(position, worldRotation, scale);
                LevelMapAuthoring.ReleaseInBoxForActiveSpawners(
                    localToWorld, cache.Bounds, cache.Vertices, cache.Triangles,
                    pushDirection, saw.MaxReleasedBlockVelocity);
            }
        }
    }
}
