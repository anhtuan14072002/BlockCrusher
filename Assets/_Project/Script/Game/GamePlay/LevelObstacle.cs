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

[DisallowMultipleComponent]
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

    private void OnEnable()
    {
        if (!ActiveObstacles.Contains(this))
            ActiveObstacles.Add(this);
    }

    private void OnDisable()
    {
        ActiveObstacles.Remove(this);
        DisposePhysics();
    }

    internal static void MaskSpawnCells(NativeArray<byte> cellSolid, Transform gridTransform, Vector3 offset,
        int gridWidth, int gridHeight, float cellSize, float colliderDepth)
    {
        LevelObstacle[] obstacles =
            FindObjectsByType<LevelObstacle>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (obstacles.Length == 0)
            return;

        Vector3 scale = gridTransform.lossyScale;
        float3 cellWorldSize = new float3(
            cellSize * Mathf.Abs(scale.x),
            cellSize * Mathf.Abs(scale.y),
            colliderDepth * Mathf.Abs(scale.z));
        using BlobAssetReference<ColliderBlob> cellCollider = Unity.Physics.BoxCollider.Create(new BoxGeometry
        {
            Center = float3.zero,
            Size = cellWorldSize,
            Orientation = quaternion.identity,
            BevelRadius = 0f
        });
        quaternion rotation = ToQuaternion(gridTransform.rotation);
        using NativeList<PhysicsRaycastHit> rayHits = new NativeList<PhysicsRaycastHit>(16, Allocator.Temp);

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                int cellIndex = y * gridWidth + x;
                if (cellSolid[cellIndex] == 0)
                    continue;

                Vector3 worldCenter = gridTransform.TransformPoint(
                    offset + new Vector3(x * cellSize, y * cellSize, 0f));
                ColliderDistanceInput input = new ColliderDistanceInput(
                    cellCollider, 0.0001f, new RigidTransform(rotation, ToFloat3(worldCenter)));

                for (int i = 0; i < obstacles.Length; i++)
                {
                    LevelObstacle obstacle = obstacles[i];
                    if (obstacle != null && obstacle.EnsureCollider() &&
                        (obstacle._rigidBody.CalculateDistance(input) ||
                         obstacle.ContainsPoint(ToFloat3(worldCenter), rayHits)))
                    {
                        cellSolid[cellIndex] = 0;
                        break;
                    }
                }
            }
        }
    }

    internal static Vector3 ClampSawTarget(Vector3 from, Vector3 to,
        BlobAssetReference<ColliderBlob> sawCollider, Quaternion sawRotation)
    {
        Vector3 position = from;
        Vector3 remaining = to - from;
        for (int iteration = 0; iteration < 2; iteration++)
        {
            if (remaining.sqrMagnitude <= 0.00000001f)
                break;

            Vector3 target = position + remaining;
            if (!TryCastSaw(position, target, sawCollider, sawRotation, out ColliderCastHit hit))
                return target;

            Vector3 normal = ToVector3(hit.SurfaceNormal).normalized;
            position = Vector3.LerpUnclamped(position, target, hit.Fraction) + normal * 0.002f;
            remaining = target - position;

            float intoSurface = Vector3.Dot(remaining, normal);
            if (intoSurface < 0f)
                remaining -= normal * intoSurface;
        }

        return position;
    }

    private static bool TryCastSaw(Vector3 from, Vector3 to,
        BlobAssetReference<ColliderBlob> sawCollider, Quaternion sawRotation,
        out ColliderCastHit closestHit)
    {
        ColliderCastInput input = new ColliderCastInput(
            sawCollider, ToFloat3(from), ToFloat3(to), ToQuaternion(sawRotation));
        closestHit = default;
        float closestFraction = 1f;
        bool hasHit = false;

        for (int i = 0; i < ActiveObstacles.Count; i++)
        {
            LevelObstacle obstacle = ActiveObstacles[i];
            if (obstacle == null || !obstacle.EnsureCollider() ||
                !obstacle._rigidBody.CastCollider(input, out ColliderCastHit hit) ||
                hit.Fraction >= closestFraction)
            {
                continue;
            }

            closestFraction = hit.Fraction;
            closestHit = hit;
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
