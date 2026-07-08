using UnityEngine;

namespace Crusher
{
    public sealed partial class CraneController
    {
        private Vector3 GetRootPosition()
        {
            if (_rootParent != null)
                return _rootParent.TransformPoint(_rootLocalPosition);

            return _joints.Count > 0 && _joints[0] != null ? _joints[0].position : transform.position;
        }

        private void ClampSawTargetToReach()
        {
            Vector3 rootPosition = GetRootPosition();
            Vector3 offset = _sawTarget - rootPosition;
            float maxReach = GetActiveReach();

            if (offset.sqrMagnitude > maxReach * maxReach)
                _sawTarget = rootPosition + offset.normalized * maxReach;
        }

        private Vector3 GetSawPosition()
        {
            return _saw != null ? _saw.position : transform.position;
        }

        private void CacheSawCutter()
        {
            if (_sawCutter == null && _saw != null)
                _sawCutter = _saw.GetComponent<SawBlockCutter>();
        }

        private float GetActiveReach()
        {
            return _activeReach;
        }

        private static Quaternion GetSegmentRotation(Vector3 direction)
        {
            return Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }
    }
}
