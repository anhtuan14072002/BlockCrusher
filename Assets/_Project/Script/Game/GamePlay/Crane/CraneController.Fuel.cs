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

            _initialFuel = Mathf.Min(_maxFuel, _initialFuel + _fuelUpgradeStep);
            _currentFuel = Mathf.Min(_maxFuel, _currentFuel + _fuelUpgradeStep);
            SetFuelAvailable(!_roundEnded);
            RefreshFuelUI();
        }

        private void ResetFuelToInitial()
        {
            _currentFuel = Mathf.Clamp(_initialFuel, 0f, _maxFuel);
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
            SawBlockCutter activeCutter = ActiveCutter;
            if (!_fuelEnabled || _currentFuel <= 0f || IsSuctionMode ||
                activeCutter == null || !activeCutter.IsCuttingBlock) return;

            _currentFuel = Mathf.Max(0f, _currentFuel - _fuelBurnRate * Time.deltaTime);
            RefreshFuelUI();

            if (_currentFuel <= 0f)
            {
                SetFuelAvailable(false);
                CraneRoundFlow.Instance.EndRound();
            }
        }

        private void SetFuelAvailable(bool isAvailable)
        {
            if (_sawCutter != null)
                _sawCutter.enabled = isAvailable;
            if (_drillCutter != null)
                _drillCutter.enabled = isAvailable;
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
