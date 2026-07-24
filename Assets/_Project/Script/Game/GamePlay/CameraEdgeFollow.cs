using UnityEngine;

public sealed class CameraEdgeFollow : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private Camera _camera;
    [SerializeField] private Vector2 _minViewport = new Vector2(0.28f, 0.24f);
    [SerializeField] private Vector2 _maxViewport = new Vector2(0.72f, 0.76f);
    [SerializeField] private float _smoothTime = 0.12f;
    [SerializeField] private float _focusSmoothTime = 0.22f;

    private Vector3 _velocity;
    private Transform _transform;
    private float _lastOrthographicSize;
    private float _lastAspect;
    private float _worldHeight;
    private float _worldWidth;
    private bool _isFocusing;

    private void Awake()
    {
        _transform = transform;
        if (_camera == null)
            _camera = GetComponent<Camera>();
        RefreshCameraSize();
    }

    private void LateUpdate()
    {
        if (_target == null || _camera == null)
            return;

        RefreshCameraSize();
        Vector3 targetPosition = _isFocusing
            ? GetCenteredCameraPosition()
            : GetCameraTargetPosition();
        Vector3 cameraPosition = _transform.position;
        float sqrDistance = (targetPosition - cameraPosition).sqrMagnitude;

        if (_isFocusing && sqrDistance <= 0.0001f)
        {
            _transform.position = targetPosition;
            _velocity = Vector3.zero;
            _isFocusing = false;
            return;
        }

        if (sqrDistance <= 0.000001f)
            return;

        float smoothTime = _isFocusing ? _focusSmoothTime : _smoothTime;
        _transform.position = Vector3.SmoothDamp(cameraPosition, targetPosition, ref _velocity, smoothTime);
    }

    public void FocusTarget()
    {
        _velocity = Vector3.zero;
        _isFocusing = true;
    }

    private Vector3 GetCenteredCameraPosition()
    {
        Vector3 viewportPosition = _camera.WorldToViewportPoint(_target.position);
        Vector3 cameraPosition = _transform.position;
        cameraPosition.x += (viewportPosition.x - 0.5f) * _worldWidth;
        cameraPosition.y += (viewportPosition.y - 0.5f) * _worldHeight;
        return cameraPosition;
    }

    private Vector3 GetCameraTargetPosition()
    {
        Vector3 viewportPosition = _camera.WorldToViewportPoint(_target.position);
        Vector3 cameraPosition = _transform.position;

        if (viewportPosition.x < _minViewport.x)
            cameraPosition.x += (viewportPosition.x - _minViewport.x) * _worldWidth;
        else if (viewportPosition.x > _maxViewport.x)
            cameraPosition.x += (viewportPosition.x - _maxViewport.x) * _worldWidth;

        if (viewportPosition.y < _minViewport.y)
            cameraPosition.y += (viewportPosition.y - _minViewport.y) * _worldHeight;
        else if (viewportPosition.y > _maxViewport.y)
            cameraPosition.y += (viewportPosition.y - _maxViewport.y) * _worldHeight;

        return cameraPosition;
    }

    private void RefreshCameraSize()
    {
        if (_camera == null && _lastOrthographicSize > 0f)
            return;

        float orthographicSize = _camera != null ? _camera.orthographicSize : 0f;
        float aspect = _camera != null ? _camera.aspect : 0f;

        if (orthographicSize == _lastOrthographicSize && aspect == _lastAspect)
            return;

        _lastOrthographicSize = orthographicSize;
        _lastAspect = aspect;
        _worldHeight = orthographicSize * 2f;
        _worldWidth = _worldHeight * aspect;
    }
}
