using UnityEngine;

namespace Crusher
{
    public sealed partial class CraneController
    {
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
    }
}
