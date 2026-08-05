using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using Unity.Transforms;
using UnityEngine;
using ColliderBlob = Unity.Physics.Collider;
using PhysicsRaycastHit = Unity.Physics.RaycastHit;

#pragma warning disable SGICE002
[RequireComponent(typeof(PhysicsShapeAuthoring))]
public sealed class LevelObstacle : MonoBehaviour
{
    private static readonly List<LevelObstacle> ActiveObstacles = new();
    private static readonly float3 InsideRayDirection = math.normalize(new float3(1f, 0.3713907f, 0.529103f));

    private PhysicsShapeAuthoring _shape;
    private BlobAssetReference<ColliderBlob> _collider;
    private RigidBody _rigidBody;
    private Aabb _bounds;
    private World _physicsWorld;
    private Entity _physicsEntity;
    private BreakableObstacle _breakable;

    private void OnEnable()
    {
        _breakable ??= GetComponent<BreakableObstacle>();
        if (!ActiveObstacles.Contains(this))
            ActiveObstacles.Add(this);

        World world = World.DefaultGameObjectInjectionWorld;
        if (world != null && world.IsCreated)
            EnsurePhysicsEntity(world);
    }

    private void OnDisable()
    {
        ActiveObstacles.Remove(this);
        DisposePhysics();
    }

    internal static void MaskSpawnCells(NativeArray<byte> cellSolid, NativeArray<byte> breakableCellMask,
        Transform gridTransform, Vector3 offset, int gridWidth, int gridHeight, Vector2 cellSize)
    {
        LevelObstacle[] obstacles =
            FindObjectsByType<LevelObstacle>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (obstacles.Length == 0)
            return;

        using NativeList<PhysicsRaycastHit> rayHits = new NativeList<PhysicsRaycastHit>(16, Allocator.Temp);

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                int cellIndex = y * gridWidth + x;
                if (cellSolid[cellIndex] == 0)
                    continue;

                Vector3 worldCenter = gridTransform.TransformPoint(
                    offset + new Vector3(x * cellSize.x, y * cellSize.y, 0f));

                for (int i = 0; i < obstacles.Length; i++)
                {
                    LevelObstacle obstacle = obstacles[i];
                    if (obstacle != null && obstacle.EnsureCollider() &&
                        obstacle.ContainsPoint(ToFloat3(worldCenter), rayHits))
                    {
                        cellSolid[cellIndex] = 0;
                        if (obstacle._breakable != null)
                            breakableCellMask[cellIndex] = 1;
                        break;
                    }
                }
            }
        }
    }

    internal static Vector3 ClampSawTarget(Vector3 from, Vector3 to,
        BlobAssetReference<ColliderBlob> sawCollider, Quaternion sawRotation,
        bool canBreakBreakables, float damage, Vector3 sawDirection, out bool damagedObstacle,
        out bool hitBreakableObstacle)
    {
        damagedObstacle = canBreakBreakables && DamageBreakableSawContact(
            from, to, sawCollider, sawRotation, damage, sawDirection);
        hitBreakableObstacle = false;
        Vector3 position = from;
        Vector3 remaining = to - from;
        for (int iteration = 0; iteration < 2; iteration++)
        {
            if (remaining.sqrMagnitude <= 0.00000001f)
                break;

            Vector3 target = position + remaining;
            if (!TryCastSaw(position, target, sawCollider, sawRotation, !canBreakBreakables,
                    out ColliderCastHit hit, out bool hitBreakable))
                return target;
            hitBreakableObstacle |= hitBreakable;

            Vector3 normal = ToVector3(hit.SurfaceNormal).normalized;
            position = Vector3.LerpUnclamped(position, target, hit.Fraction) + normal * 0.002f;
            remaining = target - position;

            float intoSurface = Vector3.Dot(remaining, normal);
            if (intoSurface < 0f)
                remaining -= normal * intoSurface;
        }

        return position;
    }

    private static bool DamageBreakableSawContact(Vector3 from, Vector3 to,
        BlobAssetReference<ColliderBlob> sawCollider, Quaternion sawRotation, float damage, Vector3 sawDirection)
    {
        ColliderCastInput castInput = new ColliderCastInput(
            sawCollider, ToFloat3(from), ToFloat3(to), ToQuaternion(sawRotation));
        ColliderDistanceInput distanceInput = new ColliderDistanceInput(
            sawCollider, 0f, new RigidTransform(ToQuaternion(sawRotation), ToFloat3(to)));

        for (int i = 0; i < ActiveObstacles.Count; i++)
        {
            LevelObstacle obstacle = ActiveObstacles[i];
            if (obstacle == null || obstacle._breakable == null || !obstacle.EnsureCollider())
                continue;

            if (!obstacle._rigidBody.CastCollider(castInput) &&
                !obstacle._rigidBody.CalculateDistance(distanceInput))
                continue;

            obstacle._breakable.ApplySawDamage(damage, sawDirection);
            return true;
        }

        return false;
    }

    internal static bool TryGetJointChainContact(
        Vector3[] points, int segmentCount, float radius, out Vector3 contactNormal)
    {
        contactNormal = Vector3.zero;
        if (radius <= 0f)
            return false;

        for (int obstacleIndex = 0; obstacleIndex < ActiveObstacles.Count; obstacleIndex++)
        {
            LevelObstacle obstacle = ActiveObstacles[obstacleIndex];
            if (obstacle == null || !obstacle.EnsureCollider())
                continue;

            for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
            {
                if (obstacle._rigidBody.CheckCapsule(
                        ToFloat3(points[segmentIndex]),
                        ToFloat3(points[segmentIndex + 1]),
                        radius,
                        CollisionFilter.Default))
                {
                    NativeList<DistanceHit> hits = new NativeList<DistanceHit>(4, Allocator.Temp);
                    obstacle._rigidBody.OverlapCapsule(
                        ToFloat3(points[segmentIndex]),
                        ToFloat3(points[segmentIndex + 1]),
                        radius,
                        ref hits,
                        CollisionFilter.Default);

                    float deepestDistance = float.MaxValue;
                    for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
                    {
                        DistanceHit hit = hits[hitIndex];
                        if (hit.Distance >= deepestDistance)
                            continue;

                        deepestDistance = hit.Distance;
                        contactNormal = ToVector3(hit.SurfaceNormal).normalized;
                    }

                    hits.Dispose();
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryCastSaw(Vector3 from, Vector3 to,
        BlobAssetReference<ColliderBlob> sawCollider, Quaternion sawRotation, bool includeBreakable,
        out ColliderCastHit closestHit, out bool hitBreakableObstacle)
    {
        ColliderCastInput input = new ColliderCastInput(
            sawCollider, ToFloat3(from), ToFloat3(to), ToQuaternion(sawRotation));
        closestHit = default;
        float closestFraction = 1f;
        bool hasHit = TryCastObstacle(input, ref closestHit, ref closestFraction, includeBreakable,
            out LevelObstacle hitObstacle);
        hitBreakableObstacle = hasHit && hitObstacle != null && hitObstacle._breakable != null;
        return hasHit;
    }

    internal static bool TryCastTool(ColliderCastInput input,
        ref ColliderCastHit closestHit, ref float closestFraction)
    {
        return TryCastObstacle(input, ref closestHit, ref closestFraction, true, out _);
    }

    private static bool TryCastObstacle(ColliderCastInput input,
        ref ColliderCastHit closestHit, ref float closestFraction, bool includeBreakable,
        out LevelObstacle hitObstacle)
    {
        bool hasHit = false;
        hitObstacle = null;

        for (int i = 0; i < ActiveObstacles.Count; i++)
        {
            LevelObstacle obstacle = ActiveObstacles[i];
            if (obstacle == null || (!includeBreakable && obstacle._breakable != null) ||
                !obstacle.EnsureCollider() ||
                !obstacle._rigidBody.CastCollider(input, out ColliderCastHit hit) ||
                hit.Fraction >= closestFraction)
            {
                continue;
            }

            closestFraction = hit.Fraction;
            closestHit = hit;
            hitObstacle = obstacle;
            hasHit = true;
        }

        return hasHit;
    }

    internal static void EnsurePhysicsEntities(World world)
    {
        for (int i = 0; i < ActiveObstacles.Count; i++)
        {
            LevelObstacle obstacle = ActiveObstacles[i];
            if (obstacle != null)
                obstacle.EnsurePhysicsEntity(world);
        }
    }

    private bool EnsureCollider()
    {
        if (_collider.IsCreated)
            return true;

        _shape ??= GetComponent<PhysicsShapeAuthoring>();
        if (_shape.ShapeType != ShapeType.Mesh)
            return false;

        using NativeList<float3> vertices = new NativeList<float3>(Allocator.Temp);
        using NativeList<int3> triangles = new NativeList<int3>(Allocator.Temp);
        _shape.GetMeshProperties(vertices, triangles);
        if (vertices.Length == 0 || triangles.Length == 0)
            return false;

        NativeArray<float3> vertexArray = vertices.AsArray();
        for (int i = 0; i < vertexArray.Length; i++)
        {
            Vector3 world = transform.TransformPoint(vertexArray[i]);
            vertexArray[i] = ToFloat3(world);
        }

        _collider = Unity.Physics.MeshCollider.Create(vertexArray, triangles.AsArray());
        _rigidBody = new RigidBody
        {
            Collider = _collider,
            WorldFromBody = RigidTransform.identity,
            Scale = 1f
        };
        _bounds = _rigidBody.CalculateAabb();
        return _collider.IsCreated;
    }

    // Surface overlap misses cells fully enclosed by a mesh, so use odd-even ray crossings for the interior.
    private bool ContainsPoint(float3 point, NativeList<PhysicsRaycastHit> hits)
    {
        if (!_bounds.Contains(point))
            return false;

        hits.Clear();
        float rayLength = math.length(_bounds.Extents) + 1f;
        RaycastInput input = default;
        input.Start = point + InsideRayDirection * rayLength;
        input.End = point;
        input.Filter = CollisionFilter.Default;
        if (!_rigidBody.CastRay(input, ref hits))
            return false;

        int uniqueHitCount = 0;
        for (int i = 0; i < hits.Length; i++)
        {
            bool duplicate = false;
            for (int j = 0; j < i; j++)
            {
                if (math.abs(hits[i].Fraction - hits[j].Fraction) < 0.0001f)
                {
                    duplicate = true;
                    break;
                }
            }

            if (!duplicate)
                uniqueHitCount++;
        }
        return (uniqueHitCount & 1) != 0;
    }

    internal bool ContainsWorldPoint(Vector3 worldPoint)
    {
        if (!EnsureCollider())
            return false;

        using NativeList<PhysicsRaycastHit> hits = new NativeList<PhysicsRaycastHit>(16, Allocator.Temp);
        return ContainsPoint(ToFloat3(worldPoint), hits);
    }

    private void EnsurePhysicsEntity(World world)
    {
        if (!EnsureCollider())
            return;

        if (_physicsWorld == world && _physicsEntity != Entity.Null &&
            world.EntityManager.Exists(_physicsEntity))
            return;

        DestroyPhysicsEntity();
        _physicsWorld = world;
        EntityManager entityManager = world.EntityManager;
        _physicsEntity = entityManager.CreateEntity(typeof(LocalTransform), typeof(PhysicsCollider));
        entityManager.SetComponentData(_physicsEntity, LocalTransform.Identity);
        entityManager.SetComponentData(_physicsEntity, new PhysicsCollider { Value = _collider });
        entityManager.AddSharedComponent(_physicsEntity, new PhysicsWorldIndex(0));
    }

    private void DisposePhysics()
    {
        DestroyPhysicsEntity();
        if (_collider.IsCreated)
        {
            _collider.Dispose();
            _collider = default;
        }
    }

    private void DestroyPhysicsEntity()
    {
        if (_physicsWorld != null && _physicsWorld.IsCreated && _physicsEntity != Entity.Null &&
            _physicsWorld.EntityManager.Exists(_physicsEntity))
        {
            _physicsWorld.EntityManager.DestroyEntity(_physicsEntity);
        }

        _physicsWorld = null;
        _physicsEntity = Entity.Null;
    }

    [ContextMenu("Validate Obstacle Setup")]
    private void ValidateSetup()
    {
        _shape ??= GetComponent<PhysicsShapeAuthoring>();
        Debug.Assert(_shape.ShapeType == ShapeType.Mesh, "Obstacle Physics Shape must use Mesh.", this);
        Debug.Assert(EnsureCollider(), "Obstacle needs a valid render mesh for its Physics Shape.", this);
    }

    private static float3 ToFloat3(Vector3 value)
    {
        return new float3(value.x, value.y, value.z);
    }

    private static Vector3 ToVector3(float3 value)
    {
        return new Vector3(value.x, value.y, value.z);
    }

    private static quaternion ToQuaternion(Quaternion value)
    {
        return new quaternion(value.x, value.y, value.z, value.w);
    }
}
#pragma warning restore SGICE002
