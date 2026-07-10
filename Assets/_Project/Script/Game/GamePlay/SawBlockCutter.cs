using System.Collections.Generic;
using UnityEngine;

public sealed class SawBlockCutter : MonoBehaviour
{
    [SerializeField] private float _compressionForce = 9f;
    [SerializeField] private float _bladeTangentialForce = 32f;
    [SerializeField] private float _bladeSpinDirection = 1f;
    [SerializeField] private float _bladeSpinSpeed = 900f;
    [SerializeField] private float _bladePushRadius = 0.55f;
    [SerializeField] private float _maxSawVelocity = 8f;
    [SerializeField] private float _cutSweepStep = 0.08f;
    [SerializeField, Range(0f, 1f)] private float _sideDampingOnContact = 0.35f;
    [SerializeField] private float _maxBlockVelocity = 11f;
    [SerializeField] private float _resistanceDuration = 0.08f;
    [SerializeField] private Transform _bladeVisual;

    private Vector3 _previousPosition;
    private Vector3 _sawVelocity;
    private Vector3 _pressDirection;
    private float _pressSpeed;
    private float _resistanceUntil;
    private readonly Dictionary<Collider, PixelBlock> _blockCache = new Dictionary<Collider, PixelBlock>(128);
    private readonly Dictionary<Collider, TextureBlockChunk> _chunkCache = new Dictionary<Collider, TextureBlockChunk>(16);

    public bool IsResisting => Time.time < _resistanceUntil;

    private void Awake()
    {
        _bladeVisual ??= transform;
        _previousPosition = transform.position;
    }

    private void Update()
    {
        if (_bladeVisual == null || _bladeSpinSpeed == 0f)
            return;

        _bladeVisual.Rotate(0f, 0f, _bladeSpinSpeed * Mathf.Sign(_bladeSpinDirection) * Time.deltaTime, Space.Self);
    }

    private void FixedUpdate()
    {
        Vector3 previousPosition = _previousPosition;
        Vector3 currentPosition = transform.position;
        float inverseDeltaTime = Time.fixedDeltaTime > 0f ? 1f / Time.fixedDeltaTime : 0f;
        _sawVelocity = (currentPosition - previousPosition) * inverseDeltaTime;

        if (_sawVelocity.sqrMagnitude > _maxSawVelocity * _maxSawVelocity)
            _sawVelocity = _sawVelocity.normalized * _maxSawVelocity;

        Vector3 planarVelocity = new Vector3(_sawVelocity.x, _sawVelocity.y, 0f);
        _pressSpeed = planarVelocity.magnitude;
        _pressDirection = _pressSpeed > 0.0001f ? planarVelocity / _pressSpeed : Vector3.zero;

        _previousPosition = currentPosition;

        if (ReleaseAlongMovement(previousPosition, currentPosition))
            RegisterResistance();
    }

    private void OnTriggerEnter(Collider other)
    {
        ReleaseAndPush(other);
    }

    private void OnTriggerStay(Collider other)
    {
        ReleaseAndPush(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        ReleaseAndPush(collision.collider, collision.GetContact(0).point);
    }

    private void OnCollisionStay(Collision collision)
    {
        ReleaseAndPush(collision.collider, collision.GetContact(0).point);
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
        _blockCache.Clear();
        _chunkCache.Clear();
    }

    private void ReleaseAndPush(Collider other)
    {
        Vector3 contactPoint = other.ClosestPoint(transform.position);
        ReleaseAndPush(other, contactPoint);
    }

    private void ReleaseAndPush(Collider other, Vector3 contactPoint)
    {
        PixelBlock block = GetBlock(other);
        if (block == null)
        {
            TextureBlockChunk chunk = GetChunk(other);
            if (chunk != null && chunk.ReleaseAtWorld(contactPoint, _pressDirection, _pressSpeed, _compressionForce,
                    _bladeTangentialForce, _bladeSpinDirection, _bladePushRadius, _sideDampingOnContact, _maxBlockVelocity))
                RegisterResistance();

            return;
        }

        RegisterResistance();
        block.Release();

        block.ApplySawCompression(transform.position, _pressDirection, _pressSpeed, contactPoint, _compressionForce,
            _bladeTangentialForce, _bladeSpinDirection, _bladePushRadius, _sideDampingOnContact, _maxBlockVelocity);
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
            releasedAny |= TextureBlockSpawner.ReleaseAtWorldForActiveSpawners(samplePosition, _pressDirection,
                _pressSpeed, _compressionForce, _bladeTangentialForce, _bladeSpinDirection, _bladePushRadius,
                _sideDampingOnContact, _maxBlockVelocity);
        }

        return releasedAny;
    }

    private void ClearCache(Collider other)
    {
        _blockCache.Remove(other);
        _chunkCache.Remove(other);
    }

    private PixelBlock GetBlock(Collider other)
    {
        if (_blockCache.TryGetValue(other, out PixelBlock block))
            return block;

        block = other.GetComponentInParent<PixelBlock>();
        _blockCache.Add(other, block);
        return block;
    }

    private TextureBlockChunk GetChunk(Collider other)
    {
        if (_chunkCache.TryGetValue(other, out TextureBlockChunk chunk))
            return chunk;

        chunk = other.GetComponentInParent<TextureBlockChunk>();
        _chunkCache.Add(other, chunk);
        return chunk;
    }
}
