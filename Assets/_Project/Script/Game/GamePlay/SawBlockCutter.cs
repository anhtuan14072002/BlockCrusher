using UnityEngine;

public sealed class SawBlockCutter : MonoBehaviour
{
    [SerializeField] private float _releasedBlockForce = 14f;
    [SerializeField] private float _maxSawVelocity = 12f;

    private Vector3 _previousPosition;
    private Vector3 _sawVelocity;

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
        block.ApplySawMotion(_sawVelocity, contactPoint, _releasedBlockForce);
    }
}
