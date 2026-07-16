using UnityEngine;
namespace Crusher
{
    public sealed partial class CraneController
    {
        private void SolveJointsToSaw()
        {
            if (_saw == null || _activeJointCount == 0 || _solvePositions == null)
                return;
            int segmentCount = _activeJointCount;
            Vector3 rootPosition = GetRootPosition();
            Vector3 targetPosition = _sawTarget;
            Vector3 rootToTarget = targetPosition - rootPosition;
            float maxReach = GetActiveReach();
            for (int i = 0; i < _activeJointCount; i++)
                _solvePositions[i] = _joints[i] != null ? _joints[i].position : rootPosition;
            if (rootToTarget.sqrMagnitude >= maxReach * maxReach)
            {
                Vector3 direction = rootToTarget.normalized;
                _solvePositions[0] = rootPosition;
                for (int i = 1; i <= segmentCount; i++)
                    _solvePositions[i] = _solvePositions[i - 1] + direction * _segmentLengths[i - 1];
                _sawTarget = _solvePositions[segmentCount];
                ApplySolvedJoints();
                return;
            }
            _solvePositions[0] = rootPosition;
            _solvePositions[segmentCount] = targetPosition;
            for (int iteration = 0; iteration < _ikIterations; iteration++)
            {
                for (int i = segmentCount - 1; i >= 0; i--)
                {
                    Vector3 direction = (_solvePositions[i] - _solvePositions[i + 1]).normalized;
                    _solvePositions[i] = _solvePositions[i + 1] + direction * _segmentLengths[i];
                }
                _solvePositions[0] = rootPosition;
                for (int i = 1; i <= segmentCount; i++)
                {
                    Vector3 direction = (_solvePositions[i] - _solvePositions[i - 1]).normalized;
                    _solvePositions[i] = _solvePositions[i - 1] + direction * _segmentLengths[i - 1];
                }
                _solvePositions[segmentCount] = targetPosition;
            }
            ApplySolvedJoints();
        }
        private void ApplySolvedJoints()
        {
            for (int i = 0; i < _activeJointCount; i++)
            {
                Transform joint = _joints[i];
                if (joint == null)
                    continue;
                Vector3 direction = _solvePositions[i + 1] - _solvePositions[i];
                float length = direction.magnitude;
                if (length <= 0.0001f)
                    continue;
                joint.position = _solvePositions[i];
                joint.rotation = GetSegmentRotation(direction) * _jointRotationOffsets[i];
            }
            Vector3 sawDirection = _solvePositions[_activeJointCount] - _solvePositions[_activeJointCount - 1];
            _saw.position = _solvePositions[_activeJointCount];
            if (_useSawInputRotation && _lastSawMoveDirection.sqrMagnitude > 0.0001f)
                _saw.rotation = GetSegmentRotation(-_lastSawMoveDirection) * _sawRotationOffset;
            else if (sawDirection.sqrMagnitude > 0.0001f)
                _saw.rotation = GetSegmentRotation(sawDirection) * _sawRotationOffset;
        }
        private void SeedZigZagPoseToSaw()
        {
            if (_saw == null || _activeJointCount <= 1)
                return;
            Vector3 rootPosition = GetRootPosition();
            Vector3 sawPosition = _saw.position;
            float reach = GetActiveReach();
            float targetDistance = Vector3.Distance(rootPosition, sawPosition);
            if (targetDistance <= 0.0001f)
                return;
            int segmentCount = _activeJointCount;
            Vector3 forward = (sawPosition - rootPosition) / targetDistance;
            Vector3 bendNormal = GetPreferredZigZagNormal(rootPosition, forward);
            float slack = Mathf.Max(0f, reach - targetDistance);
            float amplitude = Mathf.Min(_segmentLength * 0.55f, slack * 0.5f);
            if (amplitude <= 0.0001f)
                amplitude = _segmentLength * 0.25f;
            _solvePositions[0] = rootPosition;
            for (int i = 1; i < segmentCount; i++)
            {
                float t = i / (float)segmentCount;
                float sign = (i & 1) == 0 ? -1f : 1f;
                _solvePositions[i] = Vector3.Lerp(rootPosition, sawPosition, t) + bendNormal * (amplitude * sign);
            }
            _solvePositions[segmentCount] = sawPosition;
            ApplySolvedJoints();
        }
        private Vector3 GetPreferredZigZagNormal(Vector3 rootPosition, Vector3 forward)
        {
            Vector3 normal = new Vector3(-forward.y, forward.x, 0f);
            float side = 0f;
            for (int i = 1; i < _activeJointCount; i++)
            {
                Transform joint = _joints[i];
                if (joint != null)
                    side += Vector3.Dot(joint.position - rootPosition, normal);
            }
            if (Mathf.Abs(side) > 0.0001f)
                return normal * Mathf.Sign(side);
            return normal;
        }
    }
}
