using TMPro;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Crusher
{
    public sealed partial class CraneController : MonoBehaviour
    {
        public static CraneController Instance { get; private set; }

        [Header("Input")]
        [SerializeField] private Joystick _joystick;
        [SerializeField] private Camera _movementCamera;

        [Header("Movement")]
        [SerializeField] private float _sawMoveSpeed = 3.5f;
        [SerializeField, Range(0.05f, 1f)] private float _sawContactMoveMultiplier = 0.35f;
        [SerializeField, Range(0.01f, 0.5f)] private float _sawContactMoveMultiplierStep = 0.1f;
        [SerializeField] private Vector2 _targetXBounds;
        [SerializeField] private Vector2 _targetYBounds;

        [Header("Cable")]
        [SerializeField, Min(0.1f)] private float _cableMaxLength = 4.72f;
        [SerializeField, Min(0.01f)] private float _cableLengthUpgradeStep = 1.18f;
        [SerializeField] private CraneHoseVisual _cableVisual;

        [Header("Tools")]
        [SerializeField] private Transform _saw;
        [SerializeField] private SawBlockCutter _sawCutter;
        [SerializeField] private SawBlockCutter _drillCutter;
        [SerializeField] private SuctionDevice _suctionDevice;
        [SerializeField] private Button _switchToolButton;

        [Header("Fuel Settings")]
        [SerializeField, Min(1f)] private float _maxFuel = 100f;
        [SerializeField, Min(0f)] private float _initialFuel = 30f;
        [SerializeField, Min(0f)] private float _fuelBurnRate = 1f;
        [SerializeField, Min(0f)] private float _fuelBurnRateIncreaseMultiplier = 1f;
        [SerializeField, Min(0f)] private float _fuelBurnRateSawScaleIncreaseMultiplier = 1f;
        [SerializeField, Min(0f)] private float _fuelUpgradeStep = 10f;
        [SerializeField] private Image _fuelFillMask;
        [SerializeField] private TMP_Text _fuelPercentText;

        private Quaternion _sawInputRotationOffset = Quaternion.identity;
        private Quaternion _drillLocalRotation = Quaternion.identity;
        private Transform _movementCameraTransform;

        private Vector3 _lastSawMoveDirection;
        private Vector3 _sawTarget;

        private bool _isSuctionMode;
        private bool _isDrillMode;
        private bool _hasJoystickInput;

        private void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            SetUpCraneController();
            InitializeFuel();
            InitializeRoundState();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (_switchToolButton != null)
                _switchToolButton.onClick.RemoveListener(ToggleTool);
        }

        private void Update()
        {
            if (_roundEnded)
            {
                SetJoystickActivity(false);
                return;
            }

            UpdateFuel();
            if (_roundEnded || !HasFuel)
            {
                SetJoystickActivity(false);
                return;
            }

            Vector2 input = _joystick != null ? _joystick.Direction : Vector2.zero;
            bool hasInput = input.sqrMagnitude > 0.0001f;
            SetJoystickActivity(hasInput);
            if (!hasInput)
                return;

            MoveSawTarget(input);
            ApplyToolPosition();
        }

        private void FixedUpdate()
        {
            if (_roundEnded)
                return;

            if (_suctionDevice != null)
                _suctionDevice.ProcessSuction(_isSuctionMode && _hasJoystickInput && HasFuel);
        }

        public void IncreaseCableLength()
        {
            _cableMaxLength += _cableLengthUpgradeStep;
            _cableVisual.SetMaxLength(_cableMaxLength);
        }

        public void IncreaseSawContactMoveMultiplier()
        {
            float previousMultiplier = _sawContactMoveMultiplier;
            _sawContactMoveMultiplier = Mathf.Min(1f, _sawContactMoveMultiplier + _sawContactMoveMultiplierStep);
            float speedIncreaseRate = _sawContactMoveMultiplier / previousMultiplier - 1f;
            _fuelBurnRate *= 1f + speedIncreaseRate * _fuelBurnRateIncreaseMultiplier;
        }

        public void IncreaseSawHeadScale()
        {
            float previousScale = _sawCutter.transform.localScale.x;
            _sawCutter.IncreaseSawHeadScale();
            float scaleIncreaseRate = _sawCutter.transform.localScale.x / previousScale - 1f;
            _fuelBurnRate *= 1f + scaleIncreaseRate * _fuelBurnRateSawScaleIncreaseMultiplier;
        }

        public void StartRound()
        {
            _roundEnded = false;
            SetFuelAvailable(true);
            ResetJoystick();
            _joystick.gameObject.SetActive(true);
        }

        public void StopRound()
        {
            ResetCranePose();
            if (_roundEnded)
                return;

            _roundEnded = true;
            ResetFuelToInitial();
            SetFuelAvailable(false);
            ResetJoystick();
            _joystick.gameObject.SetActive(false);
        }

        private void SetUpCraneController()
        {
            _switchToolButton.onClick.AddListener(ToggleTool);

            CacheMovementCamera();
            CacheSawInputRotationOffset();
            CacheCableVisual();
            CacheSawCutter();
            CacheDrillCutter();
            _sawTarget = GetSawPosition();
            SetToolActive(false);
        }

        private void ToggleTool()
        {
            if (_isSuctionMode)
            {
                _isSuctionMode = false;
                _isDrillMode = true;
            }
            else if (_isDrillMode)
            {
                _isDrillMode = false;
            }
            else
            {
                _isSuctionMode = true;
            }

            SetToolActive(_isSuctionMode);
        }

        private void SetToolActive(bool isSuction)
        {
            if (_sawCutter != null)
            {
                bool isSawActive = !isSuction && !_isDrillMode;
                _sawCutter.gameObject.SetActive(isSawActive);
                _sawCutter.SetSpinEnabled(isSawActive && _hasJoystickInput);
            }
            if (_drillCutter != null)
            {
                bool isDrillActive = !isSuction && _isDrillMode;
                _drillCutter.gameObject.SetActive(isDrillActive);
                _drillCutter.SetSpinEnabled(isDrillActive && _hasJoystickInput);
            }
            if (_suctionDevice != null)
                _suctionDevice.gameObject.SetActive(isSuction);
            _cableVisual.SetSuctionMode(isSuction);
            ApplyDrillRotation();
        }

        private void SetJoystickActivity(bool isActive)
        {
            _hasJoystickInput = isActive;
            if (_sawCutter != null && _sawCutter.gameObject.activeSelf)
                _sawCutter.SetSpinEnabled(isActive);
            if (_drillCutter != null && _drillCutter.gameObject.activeSelf)
                _drillCutter.SetSpinEnabled(isActive);
        }

        internal void AppendSuctionTubePath(ref FixedList512Bytes<float3> path)
        {
            _cableVisual.AppendToolPath(ref path);
        }

        private void CacheCableVisual()
        {
            if (_cableVisual == null)
                _cableVisual = GetComponent<CraneHoseVisual>();
            _cableVisual.Bind(_saw, _cableMaxLength);
        }

        private void ApplyToolPosition()
        {
            if (_saw == null)
                return;

            _saw.position = _sawTarget;
            if (_lastSawMoveDirection.sqrMagnitude <= 0.0001f)
                return;

            if (_isSuctionMode && _suctionDevice != null)
            {
                Quaternion rotation = _suctionDevice.GetMovementRotation(_saw, _lastSawMoveDirection);
                _saw.rotation = _suctionDevice.ClampToolRotation(_saw, rotation);
            }
            else
            {
                _saw.rotation = GetToolRotation(_lastSawMoveDirection) * _sawInputRotationOffset;
                ApplyDrillRotation();
            }
        }

        private void ApplyDrillRotation()
        {
            if (_drillCutter == null || _saw == null)
                return;

            // The imported drill mesh faces the opposite way in the XY gameplay plane.
            // Keep its authored 3D orientation, but flip its screen-facing direction when
            // it is the active tool so the tip follows the joystick movement.
            Quaternion directionFlip = _isDrillMode ? Quaternion.Euler(0f, 0f, 180f) : Quaternion.identity;
            _drillCutter.transform.rotation = _saw.rotation * directionFlip * _drillLocalRotation;
        }
    }
}
