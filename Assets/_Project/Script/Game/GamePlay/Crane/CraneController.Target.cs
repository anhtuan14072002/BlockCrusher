using UnityEngine;

namespace Crusher
{
    public sealed partial class CraneController
    {
        private void MoveSawTarget(Vector2 input)
        {
            float resistanceRecovery = _sawCutter != null ? _sawCutter.ResistanceRecovery : 1f;
            float moveMultiplier = Mathf.Lerp(_sawContactMoveMultiplier, 1f, resistanceRecovery);
            Vector3 moveDirection = GetSawMoveDirection(input);
            Vector3 previousTarget = _sawTarget;
            _sawTarget += moveDirection * (_sawMoveSpeed * moveMultiplier * Time.deltaTime);
            _sawTarget.x = Mathf.Clamp(_sawTarget.x, _targetXBounds.x, _targetXBounds.y);
            _sawTarget.y = Mathf.Clamp(_sawTarget.y, _targetYBounds.x, _targetYBounds.y);
            ClampSawTargetToReach();

            if (_isSuctionMode && _suctionDevice != null)
                _sawTarget = _suctionDevice.ClampSawTarget(GetSawPosition(), _sawTarget);

            Vector3 actualMoveDirection = _sawTarget - previousTarget;
            if (actualMoveDirection.sqrMagnitude > 0.0001f)
                _lastSawMoveDirection = actualMoveDirection.normalized;
        }

        private void ApplySawAtPosition(Vector3 sawPosition)
        {
            if (_saw == null || _activeJointCount == 0) return;

            _sawTarget = sawPosition;
            _saw.position = sawPosition;

            Transform lastJoint = _joints[_activeJointCount - 1];
            if (lastJoint == null) return;

            Vector3 sawDirection = sawPosition - lastJoint.position;
            if (sawDirection.sqrMagnitude > 0.0001f)
                _saw.rotation = GetSegmentRotation(sawDirection) * _sawRotationOffset;
        }
    }
}
