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

    [SerializeField] private bool _childrenAreBlocks;

    private bool _initialStateCaptured;
    private bool _initialActiveSelf;
    private bool _initialEnabled;
    private Vector3 _initialLocalPosition;
    private Quaternion _initialLocalRotation;
    private Vector3 _initialLocalScale;
    private ChildInitialState[] _initialChildren;

    private PhysicsShapeAuthoring _shape;
    private BlobAssetReference<ColliderBlob> _collider;
    private RigidBody _rigidBody;
    private RigidTransform _worldFromBody;
    private Aabb _bounds;
    private World _physicsWorld;
    private Entity _physicsEntity;

    private void Awake()
    {
        CaptureInitialState();
    }

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

    internal static void ResetPreplacedBlocks()
    {
        LevelObstacle[] obstacles =
            FindObjectsByType<LevelObstacle>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < obstacles.Length; i++)
        {
            LevelObstacle obstacle = obstacles[i];
            if (obstacle != null && obstacle._childrenAreBlocks)
                obstacle.RestoreInitialState();
        }
    }

    internal static void RegisterPreplacedBlocks(TextureBlockSpawner spawner)
    {
        LevelObstacle[] obstacles =
            FindObjectsByType<LevelObstacle>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < obstacles.Length; i++)
        {
            LevelObstacle obstacle = obstacles[i];
            if (obstacle == null || !obstacle._childrenAreBlocks)
                continue;

            Transform obstacleTransform = obstacle.transform;
            for (int childIndex = 0; childIndex < obstacleTransform.childCount; childIndex++)
            {
                Transform child = obstacleTransform.GetChild(childIndex);
                if (!child.gameObject.activeInHierarchy ||
                    !child.TryGetComponent(out PhysicsShapeAuthoring shape) || shape.ShapeType != ShapeType.Sphere ||
                    !child.TryGetComponent(out MeshRenderer renderer))
                {
                    continue;
                }

                spawner.RegisterPreplacedBlock(child, renderer);
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
            if (obstacle == null || obstacle._childrenAreBlocks || !obstacle.EnsureCollider() ||
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
        float4x4 shapeToWorld = _shape.GetShapeToWorldMatrix();
        float4x4 localToShape =
            math.mul(math.inverse(shapeToWorld), (float4x4)transform.localToWorldMatrix);

        switch (_shape.ShapeType)
        {
            case ShapeType.Box:
                _collider = Unity.Physics.BoxCollider.Create(_shape.GetBakedBoxProperties());
                break;
            case ShapeType.Capsule:
                CreateCapsuleCollider(localToShape);
                break;
            case ShapeType.ConvexHull:
                CreateConvexCollider();
                break;
            case ShapeType.Cylinder:
                _collider = Unity.Physics.CylinderCollider.Create(_shape.GetBakedCylinderProperties());
                break;
            case ShapeType.Mesh:
                CreateMeshCollider();
                break;
            case ShapeType.Plane:
                CreatePlaneCollider(localToShape);
                break;
            case ShapeType.Sphere:
                CreateSphereCollider(localToShape);
                break;
        }

        if (!_collider.IsCreated)
            return false;

        _worldFromBody = new RigidTransform(ToQuaternion(transform.rotation), ToFloat3(transform.position));
        _rigidBody = new RigidBody
        {
            Collider = _collider,
            WorldFromBody = _worldFromBody,
            Scale = 1f
        };
        _bounds = _rigidBody.CalculateAabb();
        return true;
    }

    private void CaptureInitialState()
    {
        if (!_childrenAreBlocks || _initialStateCaptured)
            return;

        _initialStateCaptured = true;
        _initialActiveSelf = gameObject.activeSelf;
        _initialEnabled = enabled;
        _initialLocalPosition = transform.localPosition;
        _initialLocalRotation = transform.localRotation;
        _initialLocalScale = transform.localScale;

        int childCount = transform.childCount;
        _initialChildren = new ChildInitialState[childCount];
        for (int i = 0; i < childCount; i++)
        {
            Transform child = transform.GetChild(i);
            child.TryGetComponent(out Renderer renderer);
            _initialChildren[i] = new ChildInitialState
            {
                Transform = child,
                LocalPosition = child.localPosition,
                LocalRotation = child.localRotation,
                LocalScale = child.localScale,
                ActiveSelf = child.gameObject.activeSelf,
                Renderer = renderer,
                RendererEnabled = renderer != null && renderer.enabled
            };
        }
    }

    private void RestoreInitialState()
    {
        CaptureInitialState();

        transform.SetLocalPositionAndRotation(_initialLocalPosition, _initialLocalRotation);
        transform.localScale = _initialLocalScale;
        for (int i = 0; i < _initialChildren.Length; i++)
        {
            ChildInitialState child = _initialChildren[i];
            if (child.Transform == null)
                continue;

            child.Transform.SetLocalPositionAndRotation(child.LocalPosition, child.LocalRotation);
            child.Transform.localScale = child.LocalScale;
            child.Transform.gameObject.SetActive(child.ActiveSelf);
            if (child.Renderer != null)
                child.Renderer.enabled = child.RendererEnabled;
        }

        gameObject.SetActive(_initialActiveSelf);
        enabled = _initialEnabled;

        if (_collider.IsCreated)
        {
            _worldFromBody = new RigidTransform(ToQuaternion(transform.rotation), ToFloat3(transform.position));
            _rigidBody.WorldFromBody = _worldFromBody;
            _bounds = _rigidBody.CalculateAabb();
        }
    }

    private void CreateCapsuleCollider(float4x4 localToShape)
    {
        CapsuleGeometryAuthoring authoring = _shape.GetCapsuleProperties();
        float3 center = math.transform(localToShape, authoring.Center);
        float3 axis = TransformVector(localToShape, math.mul(authoring.Orientation, new float3(0f, 0f, 1f)));
        float3 radiusX = TransformVector(localToShape, math.mul(authoring.Orientation, new float3(1f, 0f, 0f)));
        float3 radiusY = TransformVector(localToShape, math.mul(authoring.Orientation, new float3(0f, 1f, 0f)));
        float halfSegment = math.max(0f, authoring.Height * 0.5f - authoring.Radius);

        _collider = Unity.Physics.CapsuleCollider.Create(new CapsuleGeometry
        {
            Vertex0 = center + axis * halfSegment,
            Vertex1 = center - axis * halfSegment,
            Radius = authoring.Radius * math.max(math.length(radiusX), math.length(radiusY))
        });
    }

    private void CreateConvexCollider()
    {
        using NativeList<float3> points = new NativeList<float3>(Allocator.TempJob);
        _shape.GetBakedConvexProperties(points);
        if (points.Length < 4)
            return;

        _collider = Unity.Physics.ConvexCollider.Create(
            points.AsArray(), _shape.ConvexHullGenerationParameters.ToRunTime());
    }

    private void CreateMeshCollider()
    {
        using NativeList<float3> vertices = new NativeList<float3>(Allocator.TempJob);
        using NativeList<int3> triangles = new NativeList<int3>(Allocator.Temp);
        _shape.GetBakedMeshProperties(vertices, triangles);
        if (vertices.Length == 0 || triangles.Length == 0)
            return;

        _collider = Unity.Physics.MeshCollider.Create(vertices.AsArray(), triangles.AsArray());
    }

    private void CreatePlaneCollider(float4x4 localToShape)
    {
        _shape.GetPlaneProperties(out float3 center, out float2 size, out quaternion orientation);
        float3 halfSize = new float3(size.x * 0.5f, 0f, size.y * 0.5f);
        float3 vertex0 = center + math.mul(orientation, halfSize * new float3(-1f, 0f, 1f));
        float3 vertex1 = center + math.mul(orientation, halfSize * new float3(1f, 0f, 1f));
        float3 vertex2 = center + math.mul(orientation, halfSize * new float3(1f, 0f, -1f));
        float3 vertex3 = center + math.mul(orientation, halfSize * new float3(-1f, 0f, -1f));

        _collider = Unity.Physics.PolygonCollider.CreateQuad(
            math.transform(localToShape, vertex0),
            math.transform(localToShape, vertex1),
            math.transform(localToShape, vertex2),
            math.transform(localToShape, vertex3));
    }

    private void CreateSphereCollider(float4x4 localToShape)
    {
        SphereGeometry geometry = _shape.GetSphereProperties(out quaternion orientation);
        float radiusScale = math.max(
            math.length(TransformVector(localToShape, math.mul(orientation, new float3(1f, 0f, 0f)))),
            math.max(
                math.length(TransformVector(localToShape, math.mul(orientation, new float3(0f, 1f, 0f)))),
                math.length(TransformVector(localToShape, math.mul(orientation, new float3(0f, 0f, 1f))))));
        geometry.Center = math.transform(localToShape, geometry.Center);
        geometry.Radius *= radiusScale;
        _collider = Unity.Physics.SphereCollider.Create(geometry);
    }

    private static float3 TransformVector(float4x4 matrix, float3 vector)
    {
        return math.mul(matrix, new float4(vector, 0f)).xyz;
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
        if (_childrenAreBlocks)
        {
            DestroyPhysicsEntity();
            return;
        }

        if (!EnsureCollider())
            return;

        if (_physicsWorld == world && _physicsEntity != Entity.Null &&
            world.EntityManager.Exists(_physicsEntity))
            return;

        DestroyPhysicsEntity();
        _physicsWorld = world;
        EntityManager entityManager = world.EntityManager;
        _physicsEntity = entityManager.CreateEntity(typeof(LocalTransform), typeof(PhysicsCollider));
        entityManager.SetComponentData(_physicsEntity,
            LocalTransform.FromPositionRotation(_worldFromBody.pos, _worldFromBody.rot));
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
        Debug.Assert(EnsureCollider(), $"Obstacle needs valid {_shape.ShapeType} geometry.", this);
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

    private struct ChildInitialState
    {
        public Transform Transform;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public Vector3 LocalScale;
        public bool ActiveSelf;
        public Renderer Renderer;
        public bool RendererEnabled;
    }
}
