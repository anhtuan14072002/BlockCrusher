using UnityEngine;
namespace Crusher
{
    public sealed partial class CraneController
    {
        private void ApplyActiveJointCount()
        {
            for (int i = 0; i < _joints.Count; i++)
            {
                if (_joints[i] != null)
                    _joints[i].gameObject.SetActive(i < _activeJointCount);
            }
            RefreshActiveReach();
        }

        private void RefreshActiveSegmentData()
        {
            for (int i = 0; i < _activeJointCount; i++)
            {
                Transform joint = _joints[i];
                if (joint == null) continue;
                Vector3 direction = GetSegmentEndPosition(i) - joint.position;
                if (direction.sqrMagnitude <= 0.0001f) continue;
                _segmentLengths[i] = GetSegmentLength(i, direction.magnitude);
                _jointRotationOffsets[i] = Quaternion.Inverse(GetSegmentRotation(direction)) * joint.rotation;
            }
            CacheSawRotationOffset();
            RefreshActiveReach();
        }

        private float GetSegmentLength(int segmentIndex, float measuredLength)
        {
            if (segmentIndex < _activeJointCount - 1)
                return measuredLength;

            int previousSegmentIndex = segmentIndex - 1;
            if (previousSegmentIndex >= 0 && _segmentLengths[previousSegmentIndex] > 0.0001f)
                return _segmentLengths[previousSegmentIndex];

            return _segmentLength;
        }

        private void RefreshActiveReach()
        {
            float reach = 0f;
            for (int i = 0; i < _activeJointCount; i++)
                reach += _segmentLengths[i];

            _activeReach = reach;
        }

        private void CacheSawRotationOffset()
        {
            if (_saw == null || _activeJointCount == 0) return;

            Transform lastJoint = _joints[_activeJointCount - 1];
            if (lastJoint == null) return;

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
            if (joint == null) return null;

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
            float segmentLength = _segmentLengths[insertIndex] > 0.0001f
                ? _segmentLengths[insertIndex]
                : _segmentLength;

            insertedJoint.gameObject.SetActive(true);
            insertedJoint.position = pushedPosition;
            insertedJoint.rotation = pushedJoint.rotation;
            pushedJoint.position = sawPosition;
            pushedJoint.rotation = direction.sqrMagnitude > 0.0001f
                ? GetSegmentRotation(direction) * _jointRotationOffsets[insertIndex]
                : pushedJoint.rotation;
            
            _segmentLengths[storageIndex] = segmentLength;
            _jointRotationOffsets[storageIndex] = _jointRotationOffsets[insertIndex];
            _sawTarget = sawPosition;
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

        private int GetExistingJointCount()
        {
            int count = 0;
            for (int i = 0; i < _joints.Count; i++)
            {
                if (_joints[i] == null) break;
                count++;
            }
            return count;
        }
    }
}
