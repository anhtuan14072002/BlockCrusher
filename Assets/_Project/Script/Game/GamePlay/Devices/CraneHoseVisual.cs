using UnityEngine;

namespace Crusher
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class CraneHoseVisual : MonoBehaviour
    {
        [SerializeField] private Transform _point;
        [SerializeField] private Transform _tool;
        [SerializeField] private LineRenderer _line;
        [SerializeField, Min(0f)] private float _widthLine = 0.12f;
        [SerializeField, Min(4)] private int _segmentCount = 18;
        [SerializeField, Min(0f)] private float _gravity = 0.75f;
        [SerializeField, Range(0.8f, 1f)] private float _damping = 0.9f;
        [SerializeField, Range(2, 16)] private int _constraintIterations = 12;
        [SerializeField] private Color _cutColor = new(0.08f, 0.08f, 0.08f, 1f);
        [SerializeField] private Color _suctionColor = new(0.08f, 0.08f, 0.08f, 0.38f);

        private Vector3[] _positions;
        private Vector3[] _previousPositions;
        private float _maxLength = 4.72f;
        private float _deployedLength;
        private bool _isSuctionMode;
        private bool _initialized;

        public Vector3 StartPosition => _point.position;

        private void Awake()
        {
            ApplyWidth();
        }

        private void OnValidate()
        {
            ApplyWidth();
        }

        private void ApplyWidth()
        {
            if (_line == null) return;
            _line.widthMultiplier = _widthLine;
        }

        private void LateUpdate()
        {
            if (_point == null || _tool == null) return;
            if (!_initialized) SnapToEndpoints();

            float deltaTime = Mathf.Min(Time.deltaTime, 1f / 30f);
            Simulate(deltaTime);
            SolveLengthConstraints();
            DrawCable();
        }

        public void Bind(Transform tool, float maxLength)
        {
            _tool = tool;
            _maxLength = Mathf.Max(0.1f, maxLength);
            if (_line != null)
            {
                SnapToEndpoints();
            }
        }

        public void SetMaxLength(float maxLength)
        {
            _maxLength = Mathf.Max(0.1f, maxLength);
        }

        public void SnapToEndpoints()
        {
            if (_point == null || _tool == null) return;

            int pointCount = _segmentCount + 1;
            if (_positions == null || _positions.Length != pointCount)
            {
                _positions = new Vector3[pointCount];
                _previousPositions = new Vector3[pointCount];
            }

            Vector3 root = StartPosition;
            Vector3 tool = _tool.position;
            _deployedLength = Mathf.Min(_maxLength, Vector3.Distance(root, tool));

            for (int i = 0; i < pointCount; i++)
            {
                float t = i / (float)_segmentCount;
                Vector3 point = Vector3.Lerp(root, tool, t);
                point.z = root.z;
                _positions[i] = point;
                _previousPositions[i] = point;
            }

            _line.positionCount = pointCount;
            _initialized = true;
            DrawCable();
        }

        public void SetSuctionMode(bool isSuctionMode)
        {
            _isSuctionMode = isSuctionMode;
            SetColor(_isSuctionMode ? _suctionColor : _cutColor);
        }

        public int CopyToolToRootPath(Vector3[] destination)
        {
            if (!_initialized || destination == null || destination.Length == 0)
                return 0;

            int count = 0;
            for (int i = _segmentCount; i >= 0 && count < destination.Length; i--)
            {
                destination[count] = _positions[i];
                count++;
            }

            return count;
        }

        private void Simulate(float deltaTime)
        {
            Vector3 acceleration = Vector3.down * (_gravity * deltaTime * deltaTime);
            float planeZ = StartPosition.z;

            for (int i = 1; i < _segmentCount; i++)
            {
                Vector3 current = _positions[i];
                Vector3 velocity = (current - _previousPositions[i]) * _damping;
                _previousPositions[i] = current;
                _positions[i] = current + velocity + acceleration;
                _positions[i].z = planeZ;
            }
        }

        private void SolveLengthConstraints()
        {
            Vector3 root = StartPosition;
            Vector3 tool = _tool.position;
            tool.z = root.z;
            _deployedLength = Mathf.Min(_maxLength, Vector3.Distance(root, tool));
            float segmentLength = Mathf.Max(0.001f, _deployedLength / _segmentCount);

            for (int iteration = 0; iteration < _constraintIterations; iteration++)
            {
                _positions[0] = root;
                _positions[_segmentCount] = tool;

                for (int i = 0; i < _segmentCount; i++)
                {
                    Vector3 delta = _positions[i + 1] - _positions[i];
                    float distance = delta.magnitude;
                    if (distance <= 0.0001f) continue;

                    float error = (distance - segmentLength) / distance;
                    if (i == 0)
                    {
                        _positions[i + 1] -= delta * error;
                    }
                    else if (i + 1 == _segmentCount)
                    {
                        _positions[i] += delta * error;
                    }
                    else
                    {
                        Vector3 correction = delta * (error * 0.5f);
                        _positions[i] += correction;
                        _positions[i + 1] -= correction;
                    }
                }
            }

            _positions[0] = root;
            _positions[_segmentCount] = tool;
            _previousPositions[0] = root;
            _previousPositions[_segmentCount] = tool;
        }

        private void DrawCable()
        {
            for (int i = 0; i <= _segmentCount; i++)
                _line.SetPosition(i, _positions[i]);
        }

        private void SetColor(Color color)
        {
            if (_line == null) return;
            _line.startColor = color;
            _line.endColor = color;
        }
    }
}
