using Unity.Collections;
using Unity.Mathematics;
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
        [SerializeField] private float _tubeExitRadius = 0.08f;
        [SerializeField] private float _pathWaypointRadius = 0.08f;
        [SerializeField] private float _pathLookAhead = 0.25f;
        [SerializeField] private float _tubeRenderDepth = 0.35f;

        private CraneController _craneController;

        private void Awake()
        {
            _craneController = GetComponentInParent<CraneController>();
        }

        internal void ProcessSuction(bool allowCapture)
        {
            Transform refTransform = _suctionPoint != null ? _suctionPoint : transform;
            FixedList512Bytes<float3> suctionPath = default;
            suctionPath.Add(new float3(refTransform.position.x, refTransform.position.y, refTransform.position.z));
            if (_craneController != null)
                _craneController.AppendSuctionTubePath(ref suctionPath);

            TextureBlockSpawner.ApplySuctionForActiveSpawners(
                refTransform.position, refTransform.rotation, _suctionBoxSize,
                _suctionForce, _suctionAcceleration, _maxBlockVelocity, _arrivalDamping, _tubeExitRadius,
                _pathWaypointRadius, _pathLookAhead, _tubeRenderDepth, suctionPath, Time.fixedDeltaTime,
                allowCapture);
        }

        public Vector3 ClampSawTarget(Vector3 sawPosition, Vector3 targetSawPosition)
        {
            targetSawPosition.z = sawPosition.z;
            return targetSawPosition;
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
