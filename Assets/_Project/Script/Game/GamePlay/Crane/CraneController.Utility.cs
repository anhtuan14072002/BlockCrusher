using UnityEngine;

namespace Crusher
{
    public sealed partial class CraneController
    {
        private Vector3 GetCableStartPosition()
        {
            return _cableVisual.StartPosition;
        }

        private bool ClampSawTargetToReach()
        {
            Vector3 startPosition = GetCableStartPosition();
            Vector3 offset = _sawTarget - startPosition;
            float maxReach = GetActiveReach();

            if (offset.sqrMagnitude <= maxReach * maxReach)
                return false;

            _sawTarget = startPosition + offset.normalized * maxReach;
            return true;
        }

        private Vector3 GetSawPosition()
        {
            return _saw != null ? _saw.position : transform.position;
        }

        private void CacheMovementCamera()
        {
            if (_movementCamera == null)
                _movementCamera = Camera.main;
            if (_movementCamera != null)
                _movementCameraTransform = _movementCamera.transform;
        }

        private Vector3 GetSawMoveDirection(Vector2 input)
        {
            if (_movementCameraTransform == null)
                return new Vector3(input.x, input.y, 0f);

            Vector3 screenRight = _movementCameraTransform.right;
            screenRight.z = 0f;
            Vector3 screenUp = _movementCameraTransform.up;
            screenUp.z = 0f;
            if (screenRight.sqrMagnitude <= 0.0001f || screenUp.sqrMagnitude <= 0.0001f)
                return new Vector3(input.x, input.y, 0f);

            Vector3 moveDirection = screenRight.normalized * input.x + screenUp.normalized * input.y;
            if (moveDirection.sqrMagnitude > 1f)
                moveDirection.Normalize();
            return moveDirection;
        }

        private void CacheSawCutter()
        {
            if (_sawCutter == null && _saw != null)
                _sawCutter = _saw.GetComponent<SawBlockCutter>();
        }

        private void CacheDrillCutter()
        {
            if (_drillCutter == null && _saw != null)
            {
                Transform drill = _saw.Find("Drill");
                if (drill != null)
                    _drillCutter = drill.GetComponent<SawBlockCutter>();
            }
        }

        private SawBlockCutter ActiveToolCutter => _isDrillMode ? _drillCutter : _sawCutter;

        private void CacheSawInputRotationOffset()
        {
            if (_saw == null)
                return;

            _sawInputRotationOffset = Quaternion.Inverse(GetToolRotation(Vector3.up)) * _saw.rotation;
        }

        private float GetActiveReach()
        {
            return _cableMaxLength;
        }

        private static Quaternion GetToolRotation(Vector3 direction)
        {
            return Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }
    }
}
