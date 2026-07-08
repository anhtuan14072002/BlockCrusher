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

        private void CacheToolHeads()
        {
            if (_sawHead == null && _saw != null)
            {
                SawBlockCutter sawHead = _saw.GetComponentInChildren<SawBlockCutter>(true);
                _sawHead = sawHead != null ? sawHead.gameObject : _saw.gameObject;
            }

            if (_suctionDevice == null && _saw != null)
            {
                SuctionDevice suctionDevice = _saw.GetComponentInChildren<SuctionDevice>(true);
                if (suctionDevice != null)
                    _suctionDevice = suctionDevice.gameObject;
            }

            _suctionDeviceComponent = _suctionDevice != null ? _suctionDevice.GetComponent<SuctionDevice>() : null;
        }

        private void ApplyToolHeadState()
        {
            if (_sawHead != null)
                _sawHead.SetActive(!_useSuctionDevice);

            if (_suctionDevice != null)
                _suctionDevice.SetActive(_useSuctionDevice);
        }

        private void BindButtons()
        {
            if (_switchToolButton != null)
                _switchToolButton.onClick.AddListener(SwitchToolHead);
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
