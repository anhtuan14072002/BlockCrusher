using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
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
        private const float BlockEjectBackBias = 0.65f;
        private const float CentrifugalContactWeight = 0.35f;

        private sealed class CutMeshCache
        {
            public Mesh Mesh;
            public int SubMeshIndex;
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
            public float PlanarSpinAngularSpeed;
        }

        private readonly Dictionary<Entity, CutMeshCache> _cutMeshes = new(1);
        private readonly List<CutRequest> _cutRequests = new(2);
        private EntityQuery _cutDebrisContactQuery;
        private EntityQuery _releasedBlockContactQuery;

        protected override void OnCreate()
        {
            _cutDebrisContactQuery = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsVelocity>()
                .WithAll<LocalTransform, CutDebrisComponent, Simulate>()
                .WithDisabled<CutDebrisSuctionTransit>()
                .Build();
            _releasedBlockContactQuery = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsVelocity>()
                .WithAll<LocalTransform, ReleasedBlockComponent, Simulate>()
                .WithDisabled<SuctionTransit>()
                .Build();
        }

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
                float3 worldSpinAxis = math.rotate(toRotation, saw.BladeSpinAxis);

                _cutRequests.Add(new CutRequest
                {
                    Entity = entity,
                    Saw = saw,
                    From = from,
                    To = to,
                    Rotation = toRotation,
                    PlanarSpinAngularSpeed = EntityManager.HasComponent<SawDeviceTag>(entity)
                        ? saw.BladeSpinAngularSpeed * worldSpinAxis.z
                        : 0f
                });
            }

            for (int i = 0; i < _cutRequests.Count; i++)
            {
                CutRequest request = _cutRequests[i];
                ReleaseMapAlongMovement(
                    request.Entity, in request.Saw, request.From, request.To, request.Rotation,
                    request.PlanarSpinAngularSpeed);
            }

            JobHandle dependency = Dependency;
            for (int i = 0; i < _cutRequests.Count; i++)
            {
                CutRequest request = _cutRequests[i];
                dependency = ApplySpinContactImpulse(in request, dependency);
            }
            Dependency = dependency;
        }

        private JobHandle ApplySpinContactImpulse(in CutRequest request, JobHandle dependency)
        {
            if (math.abs(request.PlanarSpinAngularSpeed) <= 0.0001f ||
                request.Saw.BlockEjectSpeed <= 0f ||
                !_cutMeshes.TryGetValue(request.Entity, out CutMeshCache cache))
                return dependency;

            Quaternion rotation = new(
                request.Rotation.value.x, request.Rotation.value.y,
                request.Rotation.value.z, request.Rotation.value.w);
            Vector3 position = new(request.To.x, request.To.y, request.To.z);
            Vector3 scale = new(request.Saw.Scale.x, request.Saw.Scale.y, request.Saw.Scale.z);
            Matrix4x4 localToWorld = Matrix4x4.TRS(position, rotation, scale);
            Vector3 center = localToWorld.MultiplyPoint3x4(cache.Bounds.center);
            Vector3 radiusX = localToWorld.MultiplyVector(Vector3.right * cache.Bounds.extents.x);
            Vector3 radiusY = localToWorld.MultiplyVector(Vector3.up * cache.Bounds.extents.y);
            float contactRadius = Mathf.Max(
                new Vector2(radiusX.x, radiusX.y).magnitude,
                new Vector2(radiusY.x, radiusY.y).magnitude);
            if (contactRadius <= 0.0001f)
                return dependency;

            float2 contactCenter = new(center.x, center.y);
            float contactRadiusSq = contactRadius * contactRadius;
            float spinDirection = math.sign(request.PlanarSpinAngularSpeed);
            float ejectSpeed = request.Saw.BlockEjectSpeed;
            dependency = new ApplySawSpinToCutDebrisJob
            {
                Center = contactCenter,
                ContactRadiusSq = contactRadiusSq,
                SpinDirection = spinDirection,
                EjectSpeed = ejectSpeed
            }.ScheduleParallel(_cutDebrisContactQuery, dependency);
            return new ApplySawSpinToReleasedBlockJob
            {
                Center = contactCenter,
                ContactRadiusSq = contactRadiusSq,
                SpinDirection = spinDirection,
                EjectSpeed = ejectSpeed
            }.ScheduleParallel(_releasedBlockContactQuery, dependency);
        }

        private void ReleaseMapAlongMovement(Entity entity, in SawComponent saw,
            float3 from, float3 to, quaternion rotation, float planarSpinAngularSpeed)
        {
            Mesh mesh = saw.CutMesh.Value;
            if (mesh == null)
                return;

            if (!_cutMeshes.TryGetValue(entity, out CutMeshCache cache) || cache.Mesh != mesh ||
                cache.SubMeshIndex != saw.CutSubMeshIndex)
            {
                if (!DeviceMeshCollider.TryGetGeometry(mesh, saw.CutSubMeshIndex,
                        out Bounds bounds, out Vector3[] vertices, out int[] triangles))
                    return;

                cache = new CutMeshCache
                {
                    Mesh = mesh,
                    SubMeshIndex = saw.CutSubMeshIndex,
                    Bounds = bounds,
                    Vertices = vertices,
                    Triangles = triangles
                };
                _cutMeshes[entity] = cache;
            }

            Vector3 fromPosition = new(from.x, from.y, from.z);
            Vector3 toPosition = new(to.x, to.y, to.z);
            Vector3 ejectDirection = GetBlockEjectDirection(toPosition - fromPosition);
            float distance = Vector2.Distance(fromPosition, toPosition);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(saw.CutSweepStep, 0.01f)));
            Quaternion worldRotation = new(rotation.value.x, rotation.value.y, rotation.value.z, rotation.value.w);
            Vector3 scale = new(saw.Scale.x, saw.Scale.y, saw.Scale.z);
            for (int i = 1; i <= steps; i++)
            {
                Vector3 position = Vector3.Lerp(fromPosition, toPosition, i / (float)steps);
                Matrix4x4 localToWorld = Matrix4x4.TRS(position, worldRotation, scale);
                LevelMapAuthoring.ReleaseInBoxForActiveSpawners(
                    localToWorld, cache.Bounds, cache.Vertices, cache.Triangles,
                    ejectDirection, saw.BlockEjectSpeed, planarSpinAngularSpeed);
            }
        }

        private static Vector3 GetBlockEjectDirection(Vector3 movement)
        {
            movement.z = 0f;
            if (movement.sqrMagnitude <= 0.000001f)
                return Vector3.up;

            return (Vector3.up - movement.normalized * BlockEjectBackBias).normalized;
        }

        private static void ApplySpinContactImpulse(ref PhysicsVelocity velocity, float2 position,
            float maxPlanarSpeed, float2 center, float contactRadiusSq,
            float spinDirection, float ejectSpeed)
        {
            float2 offset = position - center;
            if (math.lengthsq(offset) > contactRadiusSq)
                return;

            float2 radialDirection = math.normalizesafe(offset, new float2(0f, 1f));
            float2 tangentialDirection = new(
                -radialDirection.y * spinDirection,
                radialDirection.x * spinDirection);
            float2 impulseDirection = math.normalizesafe(
                tangentialDirection + radialDirection * CentrifugalContactWeight,
                radialDirection);
            float targetSpeed = math.min(math.max(ejectSpeed, 0f), math.max(maxPlanarSpeed, 0f));
            float missingSpeed = targetSpeed - math.dot(velocity.Linear.xy, impulseDirection);
            if (missingSpeed > 0f)
                velocity.Linear.xy += impulseDirection * missingSpeed;

            float speedSq = math.lengthsq(velocity.Linear.xy);
            float maxSpeedSq = maxPlanarSpeed * maxPlanarSpeed;
            if (speedSq > maxSpeedSq && maxSpeedSq > 0f)
                velocity.Linear.xy = math.normalize(velocity.Linear.xy) * maxPlanarSpeed;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        private partial struct ApplySawSpinToCutDebrisJob : IJobEntity
        {
            public float2 Center;
            public float ContactRadiusSq;
            public float SpinDirection;
            public float EjectSpeed;

            private void Execute(ref PhysicsVelocity velocity, in LocalTransform transform,
                in CutDebrisComponent debris)
            {
                ApplySpinContactImpulse(ref velocity, transform.Position.xy, debris.MaxPlanarSpeed,
                    Center, ContactRadiusSq, SpinDirection, EjectSpeed);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        private partial struct ApplySawSpinToReleasedBlockJob : IJobEntity
        {
            public float2 Center;
            public float ContactRadiusSq;
            public float SpinDirection;
            public float EjectSpeed;

            private void Execute(ref PhysicsVelocity velocity, in LocalTransform transform,
                in ReleasedBlockComponent block)
            {
                ApplySpinContactImpulse(ref velocity, transform.Position.xy, block.MaxPlanarSpeed,
                    Center, ContactRadiusSq, SpinDirection, EjectSpeed);
            }
        }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ValidateBlockEjectDirection()
        {
            Vector3 rightDig = GetBlockEjectDirection(Vector3.right);
            Debug.Assert(rightDig.x < 0f && rightDig.y > 0f,
                "Cut debris must eject upward and behind the saw movement.");
            Debug.Assert(GetBlockEjectDirection(Vector3.zero) == Vector3.up);

            PhysicsVelocity counterClockwiseVelocity = PhysicsVelocity.Zero;
            ApplySpinContactImpulse(ref counterClockwiseVelocity, new float2(1f, 0f), 4f,
                float2.zero, 2f, 1f, 2.5f);
            Debug.Assert(counterClockwiseVelocity.Linear.x > 0f &&
                         counterClockwiseVelocity.Linear.y > 0f,
                "A spinning saw must impart tangential and centrifugal velocity to nearby blocks.");
        }
#endif
    }
}
