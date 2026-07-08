using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class SuctionDevice : MonoBehaviour
{
    [SerializeField] private Transform _suctionPoint;
    [SerializeField] private float _suctionForce = 18f;
    [SerializeField] private float _maxBlockVelocity = 8f;
    [SerializeField] private float _consumeDistance = 0.18f;
    [SerializeField] private float _movementBlockRadius = 0.25f;
    [SerializeField, Range(0f, 90f)] private float _suctionAngle = 35f;

    private const float MovementSkin = 0.01f;
    private const int SweepIterations = 6;

    private readonly Dictionary<Collider, PixelBlock> _blockCache = new Dictionary<Collider, PixelBlock>(128);
    private readonly Dictionary<PixelBlock, Rigidbody> _rigidbodyCache = new Dictionary<PixelBlock, Rigidbody>(128);
    private readonly HashSet<PixelBlock> _consumedBlocks = new HashSet<PixelBlock>();

    private void Awake()
    {
        BoxCollider suctionCollider = GetComponent<BoxCollider>();
        suctionCollider.isTrigger = true;

        if (_suctionPoint == null)
            _suctionPoint = transform;
    }

    private void OnValidate()
    {
        BoxCollider suctionCollider = GetComponent<BoxCollider>();
        if (suctionCollider != null)
            suctionCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        Pull(other);
    }

    private void OnTriggerStay(Collider other)
    {
        Pull(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (_blockCache.TryGetValue(other, out PixelBlock block))
            _rigidbodyCache.Remove(block);

        _blockCache.Remove(other);
    }

    private void OnDisable()
    {
        _blockCache.Clear();
        _rigidbodyCache.Clear();
        _consumedBlocks.Clear();
    }

    public Vector3 ClampSawTarget(Vector3 sawPosition, Vector3 targetSawPosition)
    {
        Vector3 sawDelta = targetSawPosition - sawPosition;
        sawDelta.z = 0f;
        if (sawDelta.sqrMagnitude <= 0.000001f)
            return targetSawPosition;

        if (TrySweepSawTarget(sawPosition, sawDelta, out Vector3 directTarget))
            return directTarget;

        Vector3 xDelta = new Vector3(sawDelta.x, 0f, 0f);
        Vector3 yDelta = new Vector3(0f, sawDelta.y, 0f);
        Vector3 firstDelta = Mathf.Abs(sawDelta.x) >= Mathf.Abs(sawDelta.y) ? xDelta : yDelta;
        Vector3 secondDelta = Mathf.Abs(sawDelta.x) >= Mathf.Abs(sawDelta.y) ? yDelta : xDelta;

        Vector3 slidTarget = SweepSawTarget(SweepSawTarget(sawPosition, firstDelta), secondDelta);
        Vector3 alternateTarget = SweepSawTarget(SweepSawTarget(sawPosition, secondDelta), firstDelta);

        float slidDistanceSqr = (slidTarget - sawPosition).sqrMagnitude;
        float alternateDistanceSqr = (alternateTarget - sawPosition).sqrMagnitude;
        return alternateDistanceSqr > slidDistanceSqr ? alternateTarget : slidTarget;
    }

    private void Pull(Collider other)
    {
        PixelBlock block = GetBlock(other);
        if (block == null || _consumedBlocks.Contains(block))
            return;

        block.Release();

        Vector3 suctionPosition = GetSuctionPosition();
        Vector3 toBlock = block.transform.position - suctionPosition;
        toBlock.z = 0f;
        float distance = toBlock.magnitude;
        if (distance <= 0.0001f)
            return;

        Vector3 directionToBlock = toBlock / distance;
        if (!IsInSuctionDirection(directionToBlock) || IsBlockedByTexture(directionToBlock, distance))
            return;

        if (distance <= _consumeDistance)
        {
            Consume(block);
            return;
        }

        Rigidbody blockRigidbody = GetRigidbody(block);
        if (blockRigidbody == null)
            return;

        Vector3 velocity = -directionToBlock * (_suctionForce * distance);
        blockRigidbody.linearVelocity = Vector3.ClampMagnitude(velocity, _maxBlockVelocity);
    }

    private void Consume(PixelBlock block)
    {
        if (!_consumedBlocks.Add(block))
            return;

        if (block.ReturnToPool())
            _consumedBlocks.Remove(block);
        else
            Destroy(block.gameObject);
    }

    private PixelBlock GetBlock(Collider other)
    {
        if (_blockCache.TryGetValue(other, out PixelBlock block))
            return block;

        block = other.GetComponentInParent<PixelBlock>();
        _blockCache.Add(other, block);
        return block;
    }

    private Rigidbody GetRigidbody(PixelBlock block)
    {
        if (_rigidbodyCache.TryGetValue(block, out Rigidbody blockRigidbody))
            return blockRigidbody;

        blockRigidbody = block.GetComponent<Rigidbody>();
        _rigidbodyCache.Add(block, blockRigidbody);
        return blockRigidbody;
    }

    private Vector3 GetSuctionPosition()
    {
        return _suctionPoint != null ? _suctionPoint.position : transform.position;
    }

    private Vector3 GetSuctionDirection()
    {
        Transform source = _suctionPoint != null ? _suctionPoint : transform;
        Vector3 direction = source.forward;
        direction.z = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            return direction.normalized;

        direction = source.up;
        direction.z = 0f;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
    }

    private bool IsInSuctionDirection(Vector3 directionToBlock)
    {
        float minDot = Mathf.Cos(_suctionAngle * Mathf.Deg2Rad);
        return Vector3.Dot(GetSuctionDirection(), directionToBlock) >= minDot;
    }

    private bool IsBlockedByTexture(Vector3 directionToBlock, float blockDistance)
    {
        float checkDistance = Mathf.Max(0f, blockDistance - _movementBlockRadius);
        int steps = Mathf.Max(1, Mathf.CeilToInt(checkDistance / Mathf.Max(_movementBlockRadius * 0.5f, 0.05f)));
        Vector3 suctionPosition = GetSuctionPosition();

        for (int i = 1; i <= steps; i++)
        {
            Vector3 samplePoint = suctionPosition + directionToBlock * (checkDistance * (i / (float)steps));
            if (TextureBlockSpawner.HasSolidAtWorldForActiveSpawners(samplePoint, _movementBlockRadius))
                return true;
        }

        return false;
    }

    private Vector3 SweepSawTarget(Vector3 sawPosition, Vector3 sawDelta)
    {
        TrySweepSawTarget(sawPosition, sawDelta, out Vector3 target);
        return target;
    }

    private bool TrySweepSawTarget(Vector3 sawPosition, Vector3 sawDelta, out Vector3 targetSawPosition)
    {
        targetSawPosition = sawPosition;

        if (sawDelta.sqrMagnitude <= 0.000001f)
            return true;

        Vector3 suctionStart = GetSuctionPosition();
        if (!IsSuctionPathBlocked(suctionStart, sawDelta, 1f))
        {
            targetSawPosition = sawPosition + sawDelta;
            return true;
        }

        float low = 0f;
        float high = 1f;
        for (int i = 0; i < SweepIterations; i++)
        {
            float middle = (low + high) * 0.5f;
            if (IsSuctionPathBlocked(suctionStart, sawDelta, middle))
                high = middle;
            else
                low = middle;
        }

        Vector3 safeDelta = sawDelta * low;
        float safeDistance = safeDelta.magnitude;
        if (safeDistance > MovementSkin)
            safeDelta -= safeDelta / safeDistance * MovementSkin;
        else
            safeDelta = Vector3.zero;

        targetSawPosition = sawPosition + safeDelta;
        return false;
    }

    private bool IsSuctionPathBlocked(Vector3 suctionStart, Vector3 sawDelta, float normalizedDistance)
    {
        Vector3 delta = sawDelta * normalizedDistance;
        float distance = delta.magnitude;
        if (distance <= 0.0001f)
            return false;

        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(_movementBlockRadius * 0.5f, 0.05f)));
        for (int i = 1; i <= steps; i++)
        {
            Vector3 samplePoint = suctionStart + delta * (i / (float)steps);
            if (TextureBlockSpawner.HasSolidAtWorldForActiveSpawners(samplePoint, _movementBlockRadius))
                return true;
        }

        return false;
    }
}
