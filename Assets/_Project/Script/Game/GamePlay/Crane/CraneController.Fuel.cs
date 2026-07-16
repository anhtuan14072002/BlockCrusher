using UnityEngine;

namespace Crusher
{
    public sealed partial class CraneController
    {
        private float _currentFuel;
        private int _displayedFuelPercent = -1;
        private bool _fuelEnabled;

        private bool HasFuel => !_fuelEnabled || _currentFuel > 0f;

        public void RefillFuel()
        {
            if (!_fuelEnabled) return;

            _currentFuel = _maxFuel;
            SetFuelAvailable(true);
            RefreshFuelUI();
        }

        public void IncreaseFuelCapacity()
        {
            if (!_fuelEnabled || _fuelUpgradeStep <= 0f) return;

            _maxFuel += _fuelUpgradeStep;
            _currentFuel += _fuelUpgradeStep;
            SetFuelAvailable(true);
            RefreshFuelUI();
        }

        private void InitializeFuel()
        {
            _fuelEnabled = _fuelFillMask != null;
            if (!_fuelEnabled) return;

            _currentFuel = Mathf.Clamp(_initialFuel, 0f, _maxFuel);
            SetFuelAvailable(true);
            RefreshFuelUI();
        }

        private void UpdateFuel()
        {
            if (!_fuelEnabled || _currentFuel <= 0f || _isSuctionMode ||
                _sawCutter == null || !_sawCutter.IsCuttingBlock) return;

            _currentFuel = Mathf.Max(0f, _currentFuel - _fuelBurnRate * Time.deltaTime);
            RefreshFuelUI();

            if (_currentFuel <= 0f)
                SetFuelAvailable(false);
        }

        private void SetFuelAvailable(bool isAvailable)
        {
            if (_sawCutter != null)
                _sawCutter.enabled = isAvailable;
        }

        private void RefreshFuelUI()
        {
            float normalizedFuel = _maxFuel > 0f ? _currentFuel / _maxFuel : 0f;
            _fuelFillMask.fillAmount = normalizedFuel;

            int percent = Mathf.RoundToInt(normalizedFuel * 100f);
            if (_fuelPercentText == null || percent == _displayedFuelPercent) return;

            _fuelPercentText.SetText("{0}%", percent);
            _displayedFuelPercent = percent;
        }
    }
}
