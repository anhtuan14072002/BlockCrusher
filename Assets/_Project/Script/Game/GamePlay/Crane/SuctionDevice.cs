using UnityEngine;

namespace Crusher
{
    public class SuctionDevice : MonoBehaviour
    {
        [SerializeField] private Transform _suctionPoint;
        [SerializeField] private Vector3 _suctionBoxSize = new Vector3(4f, 2f, 1f);
        [SerializeField] private float _suctionForce = 15f;
        [SerializeField] private float _destroyRadius = 0.5f;

        private Collider[] _colliders = new Collider[64];

        private void FixedUpdate()
        {
            if (!gameObject.activeInHierarchy) return;
            Transform refTransform = _suctionPoint != null ? _suctionPoint : transform;

            Vector3 boxCenter = refTransform.position + refTransform.right * (_suctionBoxSize.x / 2f);
            int count = Physics.OverlapBoxNonAlloc(boxCenter, _suctionBoxSize / 2f, _colliders, refTransform.rotation);

            for (int i = 0; i < count; i++)
            {
                if (_colliders[i].TryGetComponent<PixelBlock>(out var block))
                {
                    block.Release();

                    Vector3 directionToMouth = refTransform.position - block.transform.position;
                    float distance = directionToMouth.magnitude;

                    if (distance < _destroyRadius)
                    {
                        if (!block.ReturnToPool())
                        {
                            block.gameObject.SetActive(false);
                        }

                        continue;
                    }

                    if (block.TryGetComponent<Rigidbody>(out var rb))
                    {
                        Vector3 pullDirection = -refTransform.right;

                        rb.AddForce(pullDirection * _suctionForce, ForceMode.Acceleration);

                        Vector3 centerDirection = Vector3.ProjectOnPlane(directionToMouth, refTransform.right);
                        rb.AddForce(centerDirection * (_suctionForce * 0.5f), ForceMode.Acceleration);

                        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, pullDirection * 5f + centerDirection * 2f,
                            Time.fixedDeltaTime * 4f);
                    }
                }
            }
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