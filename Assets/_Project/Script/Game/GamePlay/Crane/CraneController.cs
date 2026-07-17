using System.Collections.Generic;
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

        [SerializeField] private Joystick _joystick;
        [SerializeField] private Camera _movementCamera;
        
        [Space] 
        [SerializeField] private float _sawMoveSpeed = 3.5f;
        [SerializeField, Range(0.05f, 1f)] private float _sawContactMoveMultiplier = 0.35f;
        [SerializeField, Range(0.01f, 0.5f)] private float _sawContactMoveMultiplierStep = 0.1f;

        [SerializeField] private Vector2 _targetXBounds;
        [SerializeField] private Vector2 _targetYBounds;
        
        [SerializeField] private GameObject _jointPrefab;
        [SerializeField] private int _ikIterations = 16;
        [SerializeField] private int _activeJointCount = 4;
        [SerializeField] private int _maxJointCount = 7;
        
        [SerializeField] private float _segmentLength = 1.15f;
        
        [SerializeField] private List<Transform> _joints = new();
        [SerializeField] private Transform _saw;
        
        [SerializeField] private SawBlockCutter _sawCutter;
        [SerializeField] private SuctionDevice _suctionDevice;

        [SerializeField] private Button _switchToolButton;
        [SerializeField] private Button _addJointButton;

        [Header("Fuel Settings")]
        [SerializeField, Min(1f)] private float _maxFuel = 30f;
        [SerializeField, Min(0f)] private float _initialFuel = 30f;
        [SerializeField, Min(0f)] private float _fuelBurnRate = 1f;
        [SerializeField, Min(0f)] private float _fuelBurnRateIncreaseMultiplier = 1f;
        [SerializeField, Min(0f)] private float _fuelBurnRateSawScaleIncreaseMultiplier = 1f;
        [SerializeField, Min(0f)] private float _fuelUpgradeStep = 10f;
        [SerializeField] private Image _fuelFillMask;
        [SerializeField] private TMP_Text _fuelPercentText;

        private Quaternion _sawRotationOffset = Quaternion.identity;
        private Quaternion _sawInputRotationOffset = Quaternion.identity;
        private Quaternion[] _jointRotationOffsets;
        private Transform _movementCameraTransform;
        private Transform _rootParent;
        private Vector3[] _solvePositions;
        private Vector3 _rootLocalPosition;
        private float[] _segmentLengths;

        private Vector3 _lastSawMoveDirection;
        private Vector3 _sawTarget;
        private float _activeReach;

        private bool _isSuctionMode;
        private bool _useSawInputRotation = true;

        private void Awake()
        {
            Instance = this;
/*#if UNITY_EDITOR
            Application.targetFrameRate = 120;
#else
            Application.targetFrameRate = 60;
#endif*/
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
            {
                _switchToolButton.onClick.RemoveListener(ToggleTool);
            }
        }

        private void Update()
        {
            if (_roundEnded) return;

            UpdateFuel();
            if (_roundEnded || !HasFuel) return;

            Vector2 input = _joystick != null ? _joystick.Direction : Vector2.zero;
            if (input.sqrMagnitude <= 0.0001f) return;

            MoveSawTarget(input);
            SolveJointsToSaw();
        }

        private void FixedUpdate()
        {
            if (_roundEnded) return;

            if (_suctionDevice != null)
                _suctionDevice.ProcessSuction(_isSuctionMode && HasFuel);
        }

        public void AddJoint()
        {
            if (_activeJointCount >= _maxJointCount) return;

            Vector3 lockedSawPosition = GetSawPosition();
            Quaternion lockedSawRotation = _saw.rotation;
            int insertIndex = _activeJointCount - 1;
            int storageIndex = _activeJointCount;
            Transform insertedJoint = GetOrCreateJoint(storageIndex);
            
            if (insertedJoint == null) return;
            _sawTarget = lockedSawPosition;
            
            InsertJointBeforeLast(insertIndex, storageIndex, insertedJoint);
            _activeJointCount++;
            ApplyActiveJointCount();
            _useSawInputRotation = false;
            SeedCoiledPoseToSaw();
            SolveJointsToSaw();
            ApplySawAtPosition(lockedSawPosition);
            _saw.rotation = lockedSawRotation;
            CacheSawRotationOffset();
            _useSawInputRotation = true;
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
            if (_roundEnded) return;

            _roundEnded = true;
            ResetFuelToInitial();
            SetFuelAvailable(false);
            ResetJoystick();
            _joystick.gameObject.SetActive(false);
        }

        private void SetUpCraneController()
        {
            _switchToolButton.onClick.AddListener(ToggleTool);
            
            int existingJointCount = GetExistingJointCount();
            _maxJointCount = Mathf.Max(_maxJointCount, existingJointCount);
            _activeJointCount = existingJointCount > 0 ? Mathf.Clamp(_activeJointCount, 1, existingJointCount) : 0;

            CacheMovementCamera();
            AllocateSolverBuffers();
            CacheFixedRoot();
            CacheModelPoseOffsets();
            DetachSawFromJoints();
            CacheSawInputRotationOffset();
            ApplyActiveJointCount();
            CacheSawCutter();
            _sawTarget = GetSawPosition();
            SetToolActive(false);
        }

        private void ToggleTool()
        {
            _isSuctionMode = !_isSuctionMode;
            SetToolActive(_isSuctionMode);
        }
        
        private void SetToolActive(bool isSuction)
        {
            if (_sawCutter != null) _sawCutter.gameObject.SetActive(!isSuction);
            if (_suctionDevice != null) _suctionDevice.gameObject.SetActive(isSuction);
        }

        internal void AppendSuctionTubePath(ref FixedList512Bytes<float3> path)
        {
            int lastJointIndex = Mathf.Min(_activeJointCount, _joints.Count) - 1;
            for (int i = lastJointIndex; i >= 0; i--)
            {
                Transform joint = _joints[i];
                if (joint != null)
                    path.Add(new float3(joint.position.x, joint.position.y, joint.position.z));
            }
        }
    }
}
