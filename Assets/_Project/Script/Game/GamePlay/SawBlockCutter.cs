using System.Collections.Generic;
using UnityEngine;

public sealed class SawBlockCutter : MonoBehaviour
{
    [SerializeField] private float _compressionForce = 9f;
    [SerializeField] private float _bladeTangentialForce = 6f;
    [SerializeField] private float _bladeSpinDirection = 1f;
    [SerializeField] private float _bladePushRadius = 0.55f;
    [SerializeField] private float _maxSawVelocity = 8f;
    [SerializeField, Range(0f, 1f)] private float _sideDampingOnContact = 0.35f;
    [SerializeField] private float _maxBlockVelocity = 4f;
    [SerializeField] private float _resistanceDuration = 0.08f;

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
        _previousPosition = transform.position;
    }

    private void FixedUpdate()
    {
        Vector3 currentPosition = transform.position;
        float inverseDeltaTime = Time.fixedDeltaTime > 0f ? 1f / Time.fixedDeltaTime : 0f;
        _sawVelocity = (currentPosition - _previousPosition) * inverseDeltaTime;

        if (_sawVelocity.sqrMagnitude > _maxSawVelocity * _maxSawVelocity)
            _sawVelocity = _sawVelocity.normalized * _maxSawVelocity;

        Vector3 planarVelocity = new Vector3(_sawVelocity.x, _sawVelocity.y, 0f);
        _pressSpeed = planarVelocity.magnitude;
        _pressDirection = _pressSpeed > 0.0001f ? planarVelocity / _pressSpeed : Vector3.zero;

        _previousPosition = currentPosition;

        if (TextureBlockSpawner.ReleaseAtWorldForActiveSpawners(currentPosition, _pressDirection, _pressSpeed, _compressionForce,
                _bladeTangentialForce, _bladeSpinDirection, _bladePushRadius, _sideDampingOnContact, _maxBlockVelocity))
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
