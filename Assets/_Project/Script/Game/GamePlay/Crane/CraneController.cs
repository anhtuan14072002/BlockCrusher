using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Crusher
{
    public sealed partial class CraneController : MonoBehaviour
    {
        [SerializeField] private Joystick _joystick;
        [SerializeField] private Button _addJointButton;
        [SerializeField] private Button _switchToolButton;

        [Space] [SerializeField] private float _sawMoveSpeed = 3.5f;
        [FormerlySerializedAs("_sawContactMoveMultiplier")]
        [SerializeField, Range(0.05f, 1f)] private float _sawCrowdedMoveMultiplier = 0.2f;
        [SerializeField, Min(1)] private int _sawCrowdedBlockCount = 8;
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
        [SerializeField] private GameObject _sawHead;
        [SerializeField] private GameObject _suctionDevice;

        private Vector3[] _solvePositions;
        private float[] _segmentLengths;
        private Quaternion[] _jointRotationOffsets;
        private Quaternion _sawRotationOffset = Quaternion.identity;
        private Transform _rootParent;
        private Vector3 _rootLocalPosition;
        private Vector3 _sawTarget;
        private Vector3 _sawMoveDirection;
        private float _activeReach;
        private SuctionDevice _suctionDeviceComponent;
        private bool _useSuctionDevice;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            if (_joystick == null)
            {
                _joystick = FindFirstObjectByType<Joystick>();
                _joystick.gameObject.SetActive(true);
            }
            else
            {
                _joystick.gameObject.SetActive(true);
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
            CacheToolHeads();
            ApplyToolHeadState();
            BindButtons();

            _sawTarget = GetSawPosition();
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

        public void SwitchToolHead()
        {
            _useSuctionDevice = !_useSuctionDevice;
            ApplyToolHeadState();
        }
    }
}
