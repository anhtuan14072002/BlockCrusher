using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Crusher
{
    public sealed partial class CraneController : MonoBehaviour
    {
        [SerializeField] private Joystick _joystick;
        [SerializeField] private Camera _movementCamera;
        
        [Space] 
        [SerializeField] private float _sawMoveSpeed = 3.5f;
        [SerializeField, Range(0.05f, 1f)] private float _sawContactMoveMultiplier = 0.35f;

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

        private Quaternion _sawRotationOffset = Quaternion.identity;
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
#if UNITY_EDITOR
            Application.targetFrameRate = 120;
#else
            Application.targetFrameRate = 60;
#endif
            SetUpCraneController();
        }

        private void OnDestroy()
        {
            if (_switchToolButton != null)
            {
                _switchToolButton.onClick.RemoveListener(ToggleTool);
            }
        }

        private void Update()
        {
            Vector2 input = _joystick != null ? _joystick.Direction : Vector2.zero;
            if (input.sqrMagnitude <= 0.0001f) return;

            MoveSawTarget(input);
            SolveJointsToSaw();
        }

        public void AddJoint()
        {
            if (_activeJointCount >= _maxJointCount) return;

            Vector3 lockedSawPosition = GetSawPosition();
            int insertIndex = _activeJointCount - 1;
            int storageIndex = _activeJointCount;
            Transform insertedJoint = GetOrCreateJoint(storageIndex);
            
            if (insertedJoint == null) return;
            _sawTarget = lockedSawPosition;
            
            InsertJointBeforeLast(insertIndex, storageIndex, insertedJoint);
            _activeJointCount++;
            ApplyActiveJointCount();
            _useSawInputRotation = false;
            SeedZigZagPoseToSaw();
            SolveJointsToSaw();
            CacheSawRotationOffset();
            ApplySawAtPosition(lockedSawPosition);
            _useSawInputRotation = true;
        }

        private void SetUpCraneController()
        {
            if (_joystick != null)
                _joystick.gameObject.SetActive(true);
            
            _switchToolButton.onClick.AddListener(ToggleTool);
            
            int existingJointCount = GetExistingJointCount();
            _maxJointCount = Mathf.Max(_maxJointCount, existingJointCount);
            _activeJointCount = existingJointCount > 0 ? Mathf.Clamp(_activeJointCount, 1, existingJointCount) : 0;

            CacheMovementCamera();
            AllocateSolverBuffers();
            CacheFixedRoot();
            CacheModelPoseOffsets();
            DetachSawFromJoints();
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
    }
}