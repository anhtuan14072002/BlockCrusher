using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using UnityEngine;
using PhysicsCollider = Unity.Physics.Collider;

public sealed class SawBlockCutter : MonoBehaviour
{
    [Header("Cutting")]
    [SerializeField] private float _compressionForce = 2.5f;
    [SerializeField] private float _bladeTangentialForce = 32f;
    [SerializeField] private float _bladeSpinDirection = 1f;
    [SerializeField] private float _bladeSpinSpeed = 900f;
    [SerializeField] private float _bladePushRadius = 0.55f;
    [SerializeField] private float _maxSawVelocity = 8f;
    [SerializeField] private float _cutSweepStep = 0.08f;
    [SerializeField, Range(0f, 1f)] private float _sideDampingOnContact = 0.35f;
    [SerializeField] private float _maxBlockVelocity = 6f;
    [SerializeField, Min(0f)] private float _obstacleDamagePerSecond = 1f;
    [SerializeField] private bool _canBreakStone;
    [SerializeField, Min(0f)] private float _obstacleBounceDistance = 0.08f;

    [Header("Feedback")]
    [SerializeField] private float _resistanceDuration = 0.18f;
    [SerializeField] private float _resistanceRecoveryDuration = 0.3f;
    [SerializeField] private float _sawHeadScaleStep = 0.2f;
    [SerializeField] private float _maxSawHeadScale = 2f;
    [SerializeField] private Transform _bladeVisual;

    private Vector3 _previousPosition;
    private Vector3 _sawVelocity;
    private Vector3 _pressDirection;
    private float _pressSpeed;
    private float _resistanceUntil;
    private float _lastBlockCutTime = float.NegativeInfinity;
    private bool _hasMovedSinceEnable;
    private bool _spinEnabled;

    private readonly Dictionary<Collider, LevelMapChunk> _chunkCache = new(16);
    private PhysicsShapeAuthoring _physicsShape;
    private MeshFilter _meshFilter;
    private Vector3[] _cutVertices;
    private int[] _cutTriangles;
    private BlobAssetReference<PhysicsCollider> _obstacleQueryCollider;
    private Vector3 _obstacleQueryScale;

    public float ResistanceRecovery
    {
        get
        {
            if (Time.time <= _resistanceUntil)
                return 0f;

            return Mathf.Clamp01((Time.time - _resistanceUntil) / _resistanceRecoveryDuration);
        }
    }

    internal bool IsCuttingBlock => Time.time <= _lastBlockCutTime + Time.fixedDeltaTime * 2f;

    private void Awake()
    {
        _bladeVisual ??= transform;
        _physicsShape = GetComponent<PhysicsShapeAuthoring>();
        _meshFilter = GetComponent<MeshFilter>();
        CacheCutMesh();
        _previousPosition = transform.position;
    }

    private void OnEnable()
    {
        _previousPosition = transform.position;
        _sawVelocity = Vector3.zero;
        _pressDirection = Vector3.zero;
        _pressSpeed = 0f;
        _lastBlockCutTime = float.NegativeInfinity;
        _hasMovedSinceEnable = false;
        _spinEnabled = false;
    }

    private void LateUpdate()
    {
        if (!_spinEnabled || _bladeVisual == null || _bladeSpinSpeed == 0f)
            return;

        _bladeVisual.Rotate(0f, 0f, _bladeSpinSpeed * Mathf.Sign(_bladeSpinDirection) * Time.deltaTime, Space.Self);
    }

    internal void SetSpinEnabled(bool enabled)
    {
        _spinEnabled = enabled;
    }

    private void FixedUpdate()
    {
        Vector3 previousPosition = _previousPosition;
        Vector3 currentPosition = transform.position;
        bool movedThisStep = (currentPosition - previousPosition).sqrMagnitude > 0.000001f;
        if (movedThisStep)
            _hasMovedSinceEnable = true;
        float inverseDeltaTime = Time.fixedDeltaTime > 0f ? 1f / Time.fixedDeltaTime : 0f;
        _sawVelocity = (currentPosition - previousPosition) * inverseDeltaTime;

        if (_sawVelocity.sqrMagnitude > _maxSawVelocity * _maxSawVelocity)
            _sawVelocity = _sawVelocity.normalized * _maxSawVelocity;

        Vector3 planarVelocity = new Vector3(_sawVelocity.x, _sawVelocity.y, 0f);
        _pressSpeed = planarVelocity.magnitude;
        _pressDirection = _pressSpeed > 0.0001f ? planarVelocity / _pressSpeed : Vector3.zero;

        _previousPosition = currentPosition;

        if (movedThisStep && ReleaseAlongMovement(previousPosition, currentPosition))
        {
            RegisterBlockCut();
            RegisterResistance();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_hasMovedSinceEnable)
            return;

        ReleaseAndPush(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!_hasMovedSinceEnable)
            return;

        ReleaseAndPush(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!_hasMovedSinceEnable)
            return;

        ReleaseAndPush(collision.collider);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!_hasMovedSinceEnable)
            return;

        ReleaseAndPush(collision.collider);
    }

    private void OnCollisionExit(Collision collision)
    {
        ClearCache(collision.collider);
    }

    private void OnTriggerExit(Collider other)
    {
        ClearCache(other);
    }

    private void OnDisable()
    {
        _chunkCache.Clear();
    }

    private void OnDestroy()
    {
        if (_obstacleQueryCollider.IsCreated)
            _obstacleQueryCollider.Dispose();
    }

    public void IncreaseSawHeadScale()
    {
        float currentScale = transform.localScale.x;
        float nextScale = Mathf.Min(currentScale + _sawHeadScaleStep, _maxSawHeadScale);

        if (nextScale <= currentScale)
            return;

        float scaleMultiplier = nextScale / currentScale;
        transform.localScale *= scaleMultiplier;
    }

    internal Vector3 ClampObstacleTarget(Vector3 from, Vector3 to)
    {
        if (!EnsureObstacleQueryCollider())
            return to;

        Vector3 sawDirection = to - from;
        Vector3 clamped = LevelObstacle.ClampSawTarget(from, to, _obstacleQueryCollider, transform.rotation,
            _canBreakStone, _obstacleDamagePerSecond * Time.deltaTime, sawDirection,
            out bool damagedObstacle, out bool hitBreakableObstacle);
        if (hitBreakableObstacle && !_canBreakStone && _obstacleBounceDistance > 0f &&
            sawDirection.sqrMagnitude > 0.000001f)
        {
            clamped -= sawDirection.normalized * _obstacleBounceDistance;
        }
        if (damagedObstacle)
        {
            RegisterBlockCut();
            RegisterResistance();
        }

        return clamped;
    }

    private bool EnsureObstacleQueryCollider()
    {
        Vector3 scale = transform.lossyScale;
        if (_obstacleQueryCollider.IsCreated && scale == _obstacleQueryScale)
            return true;

        if (_obstacleQueryCollider.IsCreated)
            _obstacleQueryCollider.Dispose();

        float radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        Unity.Physics.SphereGeometry geometry;
        if (_physicsShape != null && _physicsShape.ShapeType == ShapeType.Sphere)
        {
            geometry = _physicsShape.GetSphereProperties(out quaternion _);
            geometry.Center = new float3(
                geometry.Center.x * scale.x,
                geometry.Center.y * scale.y,
                geometry.Center.z * scale.z);
            geometry.Radius *= radiusScale;
        }
        else
        {
            Bounds bounds = _meshFilter.sharedMesh.bounds;
            geometry = new Unity.Physics.SphereGeometry
            {
                Center = new float3(
                    bounds.center.x * scale.x,
                    bounds.center.y * scale.y,
                    bounds.center.z * scale.z),
                Radius = Mathf.Max(bounds.extents.x, bounds.extents.y) * radiusScale
            };
        }
        _obstacleQueryCollider = Unity.Physics.SphereCollider.Create(geometry);
        _obstacleQueryScale = scale;
        return _obstacleQueryCollider.IsCreated;
    }

    private void ReleaseAndPush(Collider other)
    {
        if (GetChunk(other) != null && ReleaseCutBoxAt(transform.position))
        {
            RegisterBlockCut();
            RegisterResistance();
        }
    }

    private void RegisterBlockCut()
    {
        _lastBlockCutTime = Time.time;
    }

    private void RegisterResistance()
    {
        _resistanceUntil = Time.time + _resistanceDuration;
    }

    private bool ReleaseAlongMovement(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        delta.z = 0f;
        float distance = delta.magnitude;
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(_cutSweepStep, 0.01f)));
        bool releasedAny = false;

        for (int i = 1; i <= steps; i++)
        {
            Vector3 samplePosition = Vector3.Lerp(from, to, i / (float)steps);
            releasedAny |= ReleaseCutBoxAt(samplePosition);
        }

        return releasedAny;
    }

    private bool ReleaseCutBoxAt(Vector3 position)
    {
        if (_cutVertices == null || _cutTriangles == null)
            return false;

        Matrix4x4 cutLocalToWorld = Matrix4x4.TRS(position, transform.rotation, transform.lossyScale);
        return LevelMapSpawner.ReleaseInBoxForActiveSpawners(cutLocalToWorld, _meshFilter.sharedMesh.bounds,
            _cutVertices, _cutTriangles,
            _pressDirection, _pressSpeed, _compressionForce, _bladeTangentialForce, _bladeSpinDirection,
            _bladePushRadius, _sideDampingOnContact, _maxBlockVelocity);
    }

    private void CacheCutMesh()
    {
        Mesh mesh = _meshFilter != null ? _meshFilter.sharedMesh : null;
        if (mesh == null || !mesh.isReadable)
        {
            Debug.LogError("Saw cut mesh must have Read/Write Enabled.", this);
            return;
        }

        _cutVertices = mesh.vertices;
        _cutTriangles = mesh.triangles;
    }

    private void ClearCache(Collider other)
    {
        _chunkCache.Remove(other);
    }

    private LevelMapChunk GetChunk(Collider other)
    {
        if (_chunkCache.TryGetValue(other, out LevelMapChunk chunk))
            return chunk;

        chunk = other.GetComponentInParent<LevelMapChunk>();
        _chunkCache.Add(other, chunk);
        return chunk;
    }
}
