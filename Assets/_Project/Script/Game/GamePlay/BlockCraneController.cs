using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class BlockCraneController : MonoBehaviour
{
    [SerializeField] private Joystick _joystick;
    [SerializeField] private Button _addJointButton;

    [Space]
    [SerializeField] private float _sawMoveSpeed = 3.5f;
    [SerializeField] private Vector2 _targetXBounds = new Vector2(-3.8f, 3.8f);
    [SerializeField] private Vector2 _targetYBounds = new Vector2(-2.6f, 6.2f);
    [SerializeField] private int _ikIterations = 16;
    
    [Space]
    [SerializeField] private int _activeJointCount = 4;
    [SerializeField] private int _maxJointCount = 7;
    [SerializeField] private float _segmentLength = 1.15f;
    [SerializeField] private GameObject _jointPrefab;
    [SerializeField] private List<Transform> _joints = new List<Transform>(8);
    [SerializeField] private Transform _saw;

    private Vector3[] _solvePositions;
    private float[] _segmentLengths;
    private Quaternion[] _jointRotationOffsets;
    private Quaternion _sawRotationOffset = Quaternion.identity;
    private Transform _rootParent;
    private Vector3 _rootLocalPosition;
    private Vector3 _sawTarget;

    private void Awake()
    {
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

        int insertIndex = _activeJointCount - 1;
        int storageIndex = _activeJointCount;
        Transform insertedJoint = GetOrCreateJoint(storageIndex);
        if (insertedJoint == null)
            return;

        InsertJointBeforeLast(insertIndex, storageIndex, insertedJoint);
        _activeJointCount++;
        ApplyActiveJointCount();
        RefreshActiveSegmentData();
        SolveJointsToSaw();
    }

    private void MoveSawTarget(Vector2 input)
    {
        _sawTarget += new Vector3(input.x, input.y, 0f) * (_sawMoveSpeed * Time.deltaTime);
        _sawTarget.x = Mathf.Clamp(_sawTarget.x, _targetXBounds.x, _targetXBounds.y);
        _sawTarget.y = Mathf.Clamp(_sawTarget.y, _targetYBounds.x, _targetYBounds.y);
        ClampSawTargetToReach();
    }

    private void SolveJointsToSaw()
    {
        if (_saw == null || _activeJointCount == 0 || _solvePositions == null)
            return;

        int segmentCount = _activeJointCount;
        Vector3 rootPosition = GetRootPosition();
        Vector3 targetPosition = _sawTarget;
        float targetDistance = Vector3.Distance(rootPosition, targetPosition);
        float maxReach = GetActiveReach();

        for (int i = 0; i < _activeJointCount; i++)
            _solvePositions[i] = _joints[i] != null ? _joints[i].position : rootPosition;

        if (targetDistance >= maxReach)
        {
            Vector3 direction = (targetPosition - rootPosition).normalized;
            _solvePositions[0] = rootPosition;

            for (int i = 1; i <= segmentCount; i++)
                _solvePositions[i] = _solvePositions[i - 1] + direction * _segmentLengths[i - 1];

            _sawTarget = _solvePositions[segmentCount];
            ApplySolvedJoints();
            return;
        }

        _solvePositions[0] = rootPosition;
        _solvePositions[segmentCount] = targetPosition;

        for (int iteration = 0; iteration < _ikIterations; iteration++)
        {
            for (int i = segmentCount - 1; i >= 0; i--)
            {
                Vector3 direction = (_solvePositions[i] - _solvePositions[i + 1]).normalized;
                _solvePositions[i] = _solvePositions[i + 1] + direction * _segmentLengths[i];
            }

            _solvePositions[0] = rootPosition;

            for (int i = 1; i <= segmentCount; i++)
            {
                Vector3 direction = (_solvePositions[i] - _solvePositions[i - 1]).normalized;
                _solvePositions[i] = _solvePositions[i - 1] + direction * _segmentLengths[i - 1];
            }

            _solvePositions[segmentCount] = targetPosition;
        }

        ApplySolvedJoints();
    }

    private void ApplyActiveJointCount()
    {
        for (int i = 0; i < _joints.Count; i++)
        {
            if (_joints[i] != null)
                _joints[i].gameObject.SetActive(i < _activeJointCount);
        }
    }

    private void ApplySolvedJoints()
    {
        for (int i = 0; i < _activeJointCount; i++)
        {
            Transform joint = _joints[i];
            if (joint == null)
                continue;

            Vector3 direction = _solvePositions[i + 1] - _solvePositions[i];
            float length = direction.magnitude;
            if (length <= 0.0001f)
                continue;

            joint.position = _solvePositions[i];
            joint.rotation = GetSegmentRotation(direction) * _jointRotationOffsets[i];
        }

        Vector3 sawDirection = _solvePositions[_activeJointCount] - _solvePositions[_activeJointCount - 1];
        _saw.position = _solvePositions[_activeJointCount];

        if (sawDirection.sqrMagnitude > 0.0001f)
            _saw.rotation = GetSegmentRotation(sawDirection) * _sawRotationOffset;
    }

    private void AllocateSolverBuffers()
    {
        _solvePositions = new Vector3[_maxJointCount + 1];
        _segmentLengths = new float[_maxJointCount];
        _jointRotationOffsets = new Quaternion[_maxJointCount];
    }

    private void CacheModelPoseOffsets()
    {
        RefreshActiveSegmentData();
    }

    private void RefreshActiveSegmentData()
    {
        for (int i = 0; i < _activeJointCount; i++)
        {
            Transform joint = _joints[i];
            if (joint == null)
                continue;

            Vector3 direction = GetSegmentEndPosition(i) - joint.position;
            if (direction.sqrMagnitude <= 0.0001f)
                continue;

            _segmentLengths[i] = direction.magnitude;
            _jointRotationOffsets[i] = Quaternion.Inverse(GetSegmentRotation(direction)) * joint.rotation;
        }

        CacheSawRotationOffset();
    }

    private void CacheSawRotationOffset()
    {
        if (_saw == null || _activeJointCount == 0)
            return;

        Transform lastJoint = _joints[_activeJointCount - 1];
        if (lastJoint == null)
            return;

        Vector3 direction = _saw.position - lastJoint.position;
        if (direction.sqrMagnitude > 0.0001f)
            _sawRotationOffset = Quaternion.Inverse(GetSegmentRotation(direction)) * _saw.rotation;
    }

    private Vector3 GetSegmentEndPosition(int jointIndex)
    {
        if (jointIndex == _activeJointCount - 1 && _saw != null)
            return _saw.position;

        int nextJointIndex = jointIndex + 1;
        if (nextJointIndex < _joints.Count && _joints[nextJointIndex] != null)
            return _joints[nextJointIndex].position;

        if (_saw != null)
            return _saw.position;

        return _joints[jointIndex].position + _joints[jointIndex].right * _segmentLength;
    }

    private Transform GetOrCreateJoint(int jointIndex)
    {
        EnsureJointSlot(jointIndex);

        if (_joints[jointIndex] != null)
            return _joints[jointIndex];

        Transform joint = CreateJointFromPrefab(jointIndex);
        if (joint == null)
            return null;

        joint.name = "Joint_" + jointIndex;
        RenameGeneratedJointChildren(joint, jointIndex);
        return joint;
    }

    private void EnsureJointSlot(int jointIndex)
    {
        while (_joints.Count <= jointIndex)
            _joints.Add(null);
    }

    private Transform CreateJointFromPrefab(int jointIndex)
    {
        if (_jointPrefab != null)
            return Instantiate(_jointPrefab, transform).transform;

        Transform source = _joints[jointIndex - 1];
        return source != null ? Instantiate(source, transform) : null;
    }

    private void InsertJointBeforeLast(int insertIndex, int storageIndex, Transform insertedJoint)
    {
        Transform pushedJoint = _joints[insertIndex];
        if (pushedJoint == null || insertedJoint == null || _saw == null)
            return;

        Vector3 pushedPosition = pushedJoint.position;
        Vector3 sawPosition = _saw.position;
        Vector3 direction = sawPosition - pushedPosition;
        float appendLength = direction.magnitude;
        if (appendLength <= 0.0001f)
            appendLength = _segmentLengths[insertIndex] > 0.0001f ? _segmentLengths[insertIndex] : _segmentLength;

        Vector3 appendDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : pushedJoint.right;
        Vector3 extendedSawPosition = sawPosition + appendDirection * appendLength;

        insertedJoint.gameObject.SetActive(true);
        insertedJoint.position = pushedPosition;
        insertedJoint.rotation = pushedJoint.rotation;

        pushedJoint.position = sawPosition;
        pushedJoint.rotation = GetSegmentRotation(appendDirection) * _jointRotationOffsets[insertIndex];

        _saw.position = extendedSawPosition;
        _sawTarget = extendedSawPosition;

        _joints[storageIndex] = pushedJoint;
        _joints[insertIndex] = insertedJoint;
    }

    private static void RenameGeneratedJointChildren(Transform joint, int jointIndex)
    {
        for (int i = 0; i < joint.childCount; i++)
        {
            Transform child = joint.GetChild(i);
            if (child.name.StartsWith("JointBlock_"))
                child.name = "JointBlock_" + jointIndex;
            else if (child.name.StartsWith("ArmBlock_"))
                child.name = "ArmBlock_" + jointIndex;
        }
    }

    private void CacheFixedRoot()
    {
        Transform root = _joints.Count > 0 ? _joints[0] : null;
        if (root == null)
            return;

        _rootParent = root.parent;
        _rootLocalPosition = root.localPosition;
    }

    private void DetachSawFromJoints()
    {
        if (_saw != null && _saw.parent != transform)
            _saw.SetParent(transform, true);
    }

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

    private float GetActiveReach()
    {
        float reach = 0f;
        for (int i = 0; i < _activeJointCount; i++)
            reach += _segmentLengths[i];

        return reach;
    }

    private int GetExistingJointCount()
    {
        int count = 0;
        for (int i = 0; i < _joints.Count; i++)
        {
            if (_joints[i] == null)
                break;

            count++;
        }

        return count;
    }

    private static Quaternion GetSegmentRotation(Vector3 direction)
    {
        return Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }
}
