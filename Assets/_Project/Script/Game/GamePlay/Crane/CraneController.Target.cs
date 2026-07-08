using UnityEngine;

namespace Crusher
{
    public sealed partial class CraneController
    {
        private void MoveSawTarget(Vector2 input)
        {
            float moveMultiplier = GetSawMoveMultiplier();
            Vector3 proposedTarget = _sawTarget + new Vector3(input.x, input.y, 0f) * (_sawMoveSpeed * moveMultiplier * Time.deltaTime);
            _sawTarget = proposedTarget;
            _sawTarget.x = Mathf.Clamp(_sawTarget.x, _targetXBounds.x, _targetXBounds.y);
            _sawTarget.y = Mathf.Clamp(_sawTarget.y, _targetYBounds.x, _targetYBounds.y);
            ClampSawTargetToReach();

            if (_useSuctionDevice && _suctionDeviceComponent != null)
                _sawTarget = _suctionDeviceComponent.ClampSawTarget(GetSawPosition(), _sawTarget);
        }

        private float GetSawMoveMultiplier()
        {
            if (_sawCutter == null || !_sawCutter.IsResisting)
                return 1f;

            float crowdRatio = Mathf.Clamp01(_sawCutter.ContactBlockCount / (float)_sawCrowdedBlockCount);
            return Mathf.Lerp(1f, _sawCrowdedMoveMultiplier, crowdRatio);
        }

        private void ApplySawAtPosition(Vector3 sawPosition)
        {
            if (_saw == null || _activeJointCount == 0)
                return;

            _sawTarget = sawPosition;
            _saw.position = sawPosition;

            Transform lastJoint = _joints[_activeJointCount - 1];
            if (lastJoint == null)
                return;

            Vector3 sawDirection = sawPosition - lastJoint.position;
            if (sawDirection.sqrMagnitude > 0.0001f)
                _saw.rotation = GetSegmentRotation(sawDirection) * _sawRotationOffset;
        }
    }
}
