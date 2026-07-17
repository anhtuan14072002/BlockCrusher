using UnityEngine;

namespace Crusher
{
    public sealed partial class CraneController
    {
        private bool _roundEnded = true;
        private Vector3 _roundStartSawLocalPosition;
        private Quaternion _initialSawLocalRotation;

        private void InitializeRoundState()
        {
            _roundStartSawLocalPosition = _saw.localPosition;
            _initialSawLocalRotation = _saw.localRotation;
            ResetJoystick();
            _joystick.gameObject.SetActive(false);
            SetFuelAvailable(false);
        }

        private void ResetJoystick()
        {
            _joystick.OnPointerUp(null);
        }

        private void ResetCranePose()
        {
            _saw.localPosition = _roundStartSawLocalPosition;
            _saw.localRotation = _initialSawLocalRotation;
            _sawTarget = _saw.position;
            _lastSawMoveDirection = Vector3.zero;
            _useSawInputRotation = false;
            SeedCoiledPoseToSaw();
            SolveJointsToSaw();
            _saw.localRotation = _initialSawLocalRotation;
            _useSawInputRotation = true;
            RefreshActiveSegmentData();
        }
    }
}
