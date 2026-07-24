using UnityEngine;

namespace Crusher
{
    public sealed partial class CraneController
    {
        private void SolveJointsToSaw()
        {
            if (_saw == null || _activeJointCount == 0 || _solvePositions == null) return;

            Vector3 previousSawPosition = GetSawPosition();
            Vector3 targetPosition = _sawTarget;
            for (int iteration = 0; iteration < 2; iteration++)
            {
                _sawTarget = SolveJointPositions(targetPosition);
                if (ApplySolvedJoints(out Vector3 contactNormal))
                    return;

                Vector3 movement = _sawTarget - previousSawPosition;
                Vector3 slideMovement = Vector3.ProjectOnPlane(movement, contactNormal);
                if (slideMovement.sqrMagnitude <= 0.00000001f ||
                    (slideMovement - movement).sqrMagnitude <= 0.00000001f)
                {
                    break;
                }

                targetPosition = previousSawPosition + slideMovement;
            }

            _sawTarget = previousSawPosition;
        }

        private Vector3 SolveJointPositions(Vector3 targetPosition)
        {
            int segmentCount = _activeJointCount;
            float maxReach = GetActiveReach();
            float straightReach = Mathf.Max(0f, maxReach - 0.001f);
            
            Vector3 rootPosition = GetRootPosition();
            Vector3 rootToTarget = targetPosition - rootPosition;
            
            for (int i = 0; i < _activeJointCount; i++)
                _solvePositions[i] = _joints[i] != null ? _joints[i].position : rootPosition;

            if (rootToTarget.sqrMagnitude >= straightReach * straightReach)
            {
                Vector3 direction = rootToTarget.normalized;
                _solvePositions[0] = rootPosition;
                
                for (int i = 1; i <= segmentCount; i++)
                    _solvePositions[i] = _solvePositions[i - 1] + direction * _segmentLengths[i - 1];
                
                return _solvePositions[segmentCount];
            }

            _solvePositions[0] = rootPosition;
            for (int iteration = 0; iteration < _ikIterations; iteration++)
            {
                _solvePositions[segmentCount] = targetPosition;
                
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
                
                if ((_solvePositions[segmentCount] - targetPosition).sqrMagnitude <= 0.000001f) break;
            }

            return _solvePositions[segmentCount];
        }

        private bool ApplySolvedJoints(out Vector3 contactNormal)
        {
            if (LevelObstacle.TryGetJointChainContact(
                    _solvePositions, _activeJointCount, _jointCollisionRadius, out contactNormal))
            {
                return false;
            }

            for (int i = 0; i < _activeJointCount; i++)
            {
                Transform joint = _joints[i];
                if (joint == null) continue;

                Vector3 direction = _solvePositions[i + 1] - _solvePositions[i];
                float length = direction.magnitude;
                if (length <= 0.0001f) continue;

                joint.position = _solvePositions[i];
                joint.rotation = GetSegmentRotation(direction) * _jointRotationOffsets[i];
            }

            Vector3 sawDirection = _solvePositions[_activeJointCount] - _solvePositions[_activeJointCount - 1];
            _saw.position = _solvePositions[_activeJointCount];
            if (_useSawInputRotation && _lastSawMoveDirection.sqrMagnitude > 0.0001f)
                _saw.rotation = GetSegmentRotation(_lastSawMoveDirection) * _sawInputRotationOffset;
            else if (sawDirection.sqrMagnitude > 0.0001f)
                _saw.rotation = GetSegmentRotation(sawDirection) * _sawRotationOffset;

            contactNormal = Vector3.zero;
            return true;
        }

        private void SeedCoiledPoseToSaw()
        {
            if (_saw == null || _activeJointCount <= 1) return;

            Vector3 rootPosition = GetRootPosition();
            Vector3 sawPosition = _saw.position;
            float reach = GetActiveReach();
            float targetDistance = Vector3.Distance(rootPosition, sawPosition);
            if (targetDistance <= 0.0001f) return;

            int segmentCount = _activeJointCount;
            Vector3 forward = (sawPosition - rootPosition) / targetDistance;
            Vector3 bendNormal = new Vector3(-forward.y, forward.x, 0f);
            float firstRadius = GetSolverSegmentLength(0);
            if (targetDistance < firstRadius) return;

            _solvePositions[0] = rootPosition;
            float remainingReach = reach - firstRadius;
            float walkedLength = firstRadius;
            float currentRadius = firstRadius;
            float totalTurn = 0f;
            for (int i = 1; i < segmentCount; i++)
            {
                float segmentLength = GetSolverSegmentLength(i);
                walkedLength += segmentLength;
                float nextRadius = Mathf.Lerp( firstRadius, targetDistance, (walkedLength - firstRadius) / remainingReach);
                totalTurn += GetCoilAngleStep(currentRadius, nextRadius, segmentLength);
                currentRadius = nextRadius;
            }

            float angle = -totalTurn;
            _solvePositions[1] = rootPosition + forward * (Mathf.Cos(angle) * firstRadius) + bendNormal * (Mathf.Sin(angle) * firstRadius);
            
            walkedLength = firstRadius;
            currentRadius = firstRadius;
            
            for (int i = 1; i < segmentCount; i++)
            {
                float segmentLength = GetSolverSegmentLength(i);
                walkedLength += segmentLength;
                float nextRadius = Mathf.Lerp(firstRadius, targetDistance, (walkedLength - firstRadius) / remainingReach);
                angle += GetCoilAngleStep(currentRadius, nextRadius, segmentLength);
                _solvePositions[i + 1] = rootPosition + forward * (Mathf.Cos(angle) * nextRadius) + bendNormal * (Mathf.Sin(angle) * nextRadius);
                currentRadius = nextRadius;
            }
            
            _solvePositions[segmentCount] = sawPosition;
            ApplySolvedJoints(out _);
        }

        private static float GetCoilAngleStep(float currentRadius, float nextRadius, float segmentLength)
        {
            float cosine = (currentRadius * currentRadius + nextRadius * nextRadius - segmentLength * segmentLength) / (2f * currentRadius * nextRadius);
            return Mathf.Acos(Mathf.Clamp(cosine, -1f, 1f));
        }

        private float GetSolverSegmentLength(int segmentIndex)
        {
            float segmentLength = _segmentLengths[segmentIndex];
            return segmentLength > 0.0001f ? segmentLength : _segmentLength;
        }
    }
}
