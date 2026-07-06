using UnityEngine;

public sealed class CameraEdgeFollow : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private Camera _camera;
    [SerializeField] private Vector2 _minViewport = new Vector2(0.28f, 0.24f);
    [SerializeField] private Vector2 _maxViewport = new Vector2(0.72f, 0.76f);
    [SerializeField] private float _smoothTime = 0.12f;

    private Vector3 _velocity;

    private void Awake()
    {
        if (_camera == null)
            _camera = GetComponent<Camera>();
    }

    private void LateUpdate()
    {
        Vector3 targetPosition = GetCameraTargetPosition();
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref _velocity, _smoothTime);
    }

    private Vector3 GetCameraTargetPosition()
    {
        if (_target == null || _camera == null)
            return transform.position;

        Vector3 viewportPosition = _camera.WorldToViewportPoint(_target.position);
        Vector3 cameraPosition = transform.position;

        float worldHeight = _camera.orthographicSize * 2f;
        float worldWidth = worldHeight * _camera.aspect;

        if (viewportPosition.x < _minViewport.x)
            cameraPosition.x += (viewportPosition.x - _minViewport.x) * worldWidth;
        else if (viewportPosition.x > _maxViewport.x)
            cameraPosition.x += (viewportPosition.x - _maxViewport.x) * worldWidth;

        if (viewportPosition.y < _minViewport.y)
            cameraPosition.y += (viewportPosition.y - _minViewport.y) * worldHeight;
        else if (viewportPosition.y > _maxViewport.y)
            cameraPosition.y += (viewportPosition.y - _maxViewport.y) * worldHeight;

        return cameraPosition;
    }
}
