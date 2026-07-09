using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Crusher
{
    public sealed partial class CraneController : MonoBehaviour
    {
        [SerializeField] private Joystick _joystick;
        [SerializeField] private Button _addJointButton;

        [Space] [SerializeField] private float _sawMoveSpeed = 3.5f;
        [SerializeField, Range(0.05f, 1f)] private float _sawContactMoveMultiplier = 0.35f;
        [SerializeField] private Vector2 _targetXBounds = new Vector2(-3.8f, 3.8f);
        [SerializeField] private Vector2 _targetYBounds = new Vector2(-2.6f, 6.2f);
        [SerializeField] private int _ikIterations = 16;

        [Space] [SerializeField] private int _activeJointCount = 4;
        [SerializeField] private int _maxJointCount = 7;
        [SerializeField] private float _segmentLength = 1.15f;
        [SerializeField] private GameObject _jointPrefab;
        [SerializeField] private List<Transform> _joints = new List<Transform>(8);
        [SerializeField] private Transform _saw;
        [SerializeField] private SawBlockCutter _sawCutter;
        [SerializeField] private SuctionDevice _suctionDevice;
        [SerializeField] private Button _switchToolButton;

        private Vector3[] _solvePositions;
        private float[] _segmentLengths;
        private Quaternion[] _jointRotationOffsets;
        private Quaternion _sawRotationOffset = Quaternion.identity;
        private Transform _rootParent;
        private Vector3 _rootLocalPosition;                                        
        private Vector3 _sawTarget;
        private float _activeReach;
        private bool _isSuctionMode;

        private void Awake()
        {
            Application.targetFrameRate = 120;
            if (_joystick == null)
            {
                _joystick = FindFirstObjectByType<Joystick>();
                _joystick.gameObject.SetActive(true);
            }
            else
            {
                _joystick.gameObject.SetActive(true);
            }

            if (_switchToolButton != null)
            {
                _switchToolButton.onClick.AddListener(ToggleTool);
            }

            int existingJointCount = GetExistingJointCount();
            _maxJointCount = Mathf.Max(_maxJointCount, existingJointCount);
            _activeJointCount = existingJointCount > 0 ? Mathf.Clamp(_activeJointCount, 1, existingJointCount) : 0;

            AllocateSolverBuffers();
            CacheFixedRoot();
            CacheModelPoseOffsets();
            DetachSawFromJoints();
            ApplyActiveJointCount();
            CacheSawCutter();

            _sawTarget = GetSawPosition();
            
            SetToolActive(false);
        }

        private void Update()
        {
            Vector2 input = _joystick != null ? _joystick.Direction : Vector2.zero;
            if (input.sqrMagnitude <= 0.0001f)
                return;

            MoveSawTarget(input);
            SolveJointsToSaw();
        }

        public void AddJoint()
        {
            if (_activeJointCount >= _maxJointCount)
                return;

            Vector3 lockedSawPosition = GetSawPosition();
            int insertIndex = _activeJointCount - 1;
            int storageIndex = _activeJointCount;
            Transform insertedJoint = GetOrCreateJoint(storageIndex);
            if (insertedJoint == null)
                return;

            _sawTarget = lockedSawPosition;
            InsertJointBeforeLast(insertIndex, storageIndex, insertedJoint);
            _activeJointCount++;
            ApplyActiveJointCount();
            SeedZigZagPoseToSaw();
            SolveJointsToSaw();
            CacheSawRotationOffset();
            ApplySawAtPosition(lockedSawPosition);
        }

        public void ToggleTool()
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
