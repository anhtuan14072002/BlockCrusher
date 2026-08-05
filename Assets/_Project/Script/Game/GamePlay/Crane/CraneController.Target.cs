using UnityEngine;

namespace Crusher
{
    public sealed partial class CraneController
    {
        private void MoveSawTarget(Vector2 input)
        {
            SawBlockCutter activeCutter = ActiveToolCutter;
            float resistanceRecovery = activeCutter != null ? activeCutter.ResistanceRecovery : 1f;
            float moveMultiplier = Mathf.Lerp(_sawContactMoveMultiplier, 1f, resistanceRecovery);
            Vector3 moveDirection = GetSawMoveDirection(input);
            Vector3 previousTarget = _sawTarget;
            Vector3 previousSawPosition = GetSawPosition();
            _sawTarget += moveDirection * (_sawMoveSpeed * moveMultiplier * Time.deltaTime);
            _sawTarget.x = Mathf.Clamp(_sawTarget.x, _targetXBounds.x, _targetXBounds.y);
            _sawTarget.y = Mathf.Clamp(_sawTarget.y, _targetYBounds.x, _targetYBounds.y);
            bool reachClamped = ClampSawTargetToReach();

            if (!_isSuctionMode && activeCutter != null)
            {
                Vector3 requestedTarget = _sawTarget;
                Vector3 clampedTarget = activeCutter.ClampObstacleTarget(previousSawPosition, requestedTarget);
                _sawTarget = clampedTarget;
            }
            else if (_suctionDevice != null)
                _sawTarget = _suctionDevice.ClampSawTarget(previousSawPosition, _sawTarget);

            // The reference tool keeps facing the joystick while collision independently projects
            // its position onto the surface. This lets the head rotate even during a blocked slide.
            Vector3 actualMoveDirection = _sawTarget - previousTarget;
            Vector3 facingDirection = _isSuctionMode
                ? moveDirection
                : reachClamped ? moveDirection : actualMoveDirection;
            if (facingDirection.sqrMagnitude > 0.00000001f)
                _lastSawMoveDirection = facingDirection.normalized;
        }

        private void ApplySawAtPosition(Vector3 sawPosition)
        {
            if (_saw == null)
                return;

            _sawTarget = sawPosition;
            _saw.position = sawPosition;
        }
    }
}
