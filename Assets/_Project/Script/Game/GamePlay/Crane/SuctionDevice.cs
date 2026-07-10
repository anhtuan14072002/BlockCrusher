using UnityEngine;

namespace Crusher
{
    public class SuctionDevice : MonoBehaviour
    {
        [SerializeField] private Transform _suctionPoint;
        [SerializeField] private Vector3 _suctionBoxSize = new Vector3(4f, 2f, 1f);
        [SerializeField] private float _suctionForce = 15f;
        [SerializeField] private float _suctionAcceleration = 32f;
        [SerializeField] private float _maxBlockVelocity = 8f;
        [SerializeField] private float _arrivalDamping = 8f;
        [SerializeField] private float _destroyRadius = 0.5f;
        [SerializeField] private float _movementBlockRadius = 0.25f;
        [SerializeField, Range(0.25f, 1f)] private float _slideProbeRadiusMultiplier = 0.55f;

        private const float MovementSkin = 0.01f;
        private const int SweepIterations = 6;
        private Collider[] _colliders = new Collider[64];

        private void FixedUpdate()
        {
            if (!gameObject.activeInHierarchy) return;
            Transform refTransform = _suctionPoint != null ? _suctionPoint : transform;

            Vector3 boxCenter = refTransform.position + refTransform.right * (_suctionBoxSize.x / 2f);
            int count = Physics.OverlapBoxNonAlloc(boxCenter, _suctionBoxSize / 2f, _colliders, refTransform.rotation);

            for (int i = 0; i < count; i++)
            {
                PixelBlock block = _colliders[i].GetComponentInParent<PixelBlock>();
                if (block != null)
                    PullBlock(refTransform, block);
            }
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
            Vector3 xTarget = SweepSawTarget(sawPosition, xDelta);
            Vector3 yTarget = SweepSawTarget(sawPosition, yDelta);

            Vector3 xMove = xTarget - sawPosition;
            Vector3 yMove = yTarget - sawPosition;
            if (xMove.sqrMagnitude <= 0.000001f && yMove.sqrMagnitude <= 0.000001f)
                return sawPosition;
            if (xMove.sqrMagnitude <= 0.000001f)
                return yTarget;
            if (yMove.sqrMagnitude <= 0.000001f)
                return xTarget;

            return Mathf.Abs(sawDelta.x) >= Mathf.Abs(sawDelta.y)
                ? SweepSawTarget(xTarget, yMove)
                : SweepSawTarget(yTarget, xMove);
        }

        private void PullBlock(Transform refTransform, PixelBlock block)
        {
            block.Release();

            Vector3 directionToMouth = refTransform.position - block.transform.position;
            directionToMouth.z = 0f;
            float distance = directionToMouth.magnitude;
            if (distance <= 0.0001f)
                return;

            Vector3 pullDirection = directionToMouth / distance;
            if (IsBlockedByTexture(refTransform.position, -pullDirection, distance))
                return;

            if (distance < _destroyRadius)
            {
                if (!block.ReturnToPool())
                    block.gameObject.SetActive(false);

                return;
            }

            if (!block.TryGetComponent<Rigidbody>(out var rb))
                return;

            Vector3 targetVelocity = pullDirection * Mathf.Min(_suctionForce * distance, _maxBlockVelocity);
            Vector3 smoothedVelocity = Vector3.MoveTowards(rb.linearVelocity, targetVelocity,
                _suctionAcceleration * Time.fixedDeltaTime);

            if (distance <= _destroyRadius * 2.5f)
                smoothedVelocity = Vector3.Lerp(smoothedVelocity, targetVelocity,
                    _arrivalDamping * Time.fixedDeltaTime);

            rb.linearVelocity = Vector3.ClampMagnitude(smoothedVelocity, _maxBlockVelocity);
        }

        private bool IsBlockedByTexture(Vector3 suctionPosition, Vector3 directionToBlock, float blockDistance)
        {
            float checkDistance = Mathf.Max(0f, blockDistance - _movementBlockRadius);
            int steps = Mathf.Max(1, Mathf.CeilToInt(checkDistance / Mathf.Max(_movementBlockRadius * 0.5f, 0.05f)));

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
                if (TextureBlockSpawner.HasSolidAtWorldForActiveSpawners(samplePoint, GetSlideProbeRadius()))
                    return true;
            }

            return false;
        }

        private Vector3 GetSuctionPosition()
        {
            return _suctionPoint != null ? _suctionPoint.position : transform.position;
        }

        private float GetSlideProbeRadius()
        {
            return _movementBlockRadius * _slideProbeRadiusMultiplier;
        }

        private void OnDrawGizmosSelected()
        {
            Transform refTransform = _suctionPoint != null ? _suctionPoint : transform;

            Vector3 boxCenter = refTransform.position + refTransform.right * (_suctionBoxSize.x / 2f);

            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Gizmos.matrix = Matrix4x4.TRS(boxCenter, refTransform.rotation, Vector3.one);
            Gizmos.DrawCube(Vector3.zero, _suctionBoxSize);
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(Vector3.zero, _suctionBoxSize);
        }
    }
}
