using UnityEngine;

public sealed class SawBlockCutter : MonoBehaviour
{
    [SerializeField] private float _compressionForce = 9f;
    [SerializeField] private float _maxSawVelocity = 8f;
    [SerializeField] private float _minPushSpeed = 0.15f;
    [SerializeField, Range(0f, 1f)] private float _sideDampingOnContact = 0.35f;
    [SerializeField] private float _maxBlockVelocity = 4f;

    private Vector3 _previousPosition;
    private Vector3 _sawVelocity;
    private Vector3 _pressDirection;
    private float _pressSpeed;

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

    private void ReleaseAndPush(Collider other)
    {
        Vector3 contactPoint = other.ClosestPoint(transform.position);
        ReleaseAndPush(other, contactPoint);
    }

    private void ReleaseAndPush(Collider other, Vector3 contactPoint)
    {
        PixelBlock block = other.GetComponentInParent<PixelBlock>();
        if (block == null)
            return;

        block.Release();

        if (_pressSpeed < _minPushSpeed)
            return;

        block.ApplySawCompression(_pressDirection, _pressSpeed, contactPoint, _compressionForce, _sideDampingOnContact, _maxBlockVelocity);
    }
}
