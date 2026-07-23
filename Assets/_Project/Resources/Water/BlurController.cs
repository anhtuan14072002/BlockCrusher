using UnityEngine;

[ExecuteInEditMode]
public class BlurController : MonoBehaviour
{
    public Camera sourceCamera;
    [Min(0)] public int iterations = 3;
    public float blurSpread = 0.6f;
    public Shader blurShader;

    private Camera _camera;
    private Material _material;

    private Material Material
    {
        get
        {
            if (_material == null && blurShader != null)
                _material = new Material(blurShader) { hideFlags = HideFlags.DontSave };
            return _material;
        }
    }

    private void OnEnable()
    {
        _camera = GetComponent<Camera>();
        SyncCamera();
    }

    private void LateUpdate()
    {
        SyncCamera();
    }

    private void SyncCamera()
    {
        if (_camera == null || sourceCamera == null)
            return;
        Transform sourceTransform = sourceCamera.transform;
        transform.SetPositionAndRotation(sourceTransform.position, sourceTransform.rotation);
        _camera.orthographic = sourceCamera.orthographic;
        _camera.orthographicSize = sourceCamera.orthographicSize;
        _camera.fieldOfView = sourceCamera.fieldOfView;
        _camera.aspect = sourceCamera.aspect;
        _camera.nearClipPlane = sourceCamera.nearClipPlane;
        _camera.farClipPlane = sourceCamera.farClipPlane;
    }

    private void FourTapCone(RenderTexture source, RenderTexture destination, int iteration)
    {
        float offset = 0.5f + iteration * blurSpread;
        Graphics.BlitMultiTap(source, destination, Material,
            new Vector2(-offset, -offset),
            new Vector2(-offset, offset),
            new Vector2(offset, offset),
            new Vector2(offset, -offset));
    }

    private void DownSample4x(RenderTexture source, RenderTexture dest)
    {
        const float offset = 1f;
        Graphics.BlitMultiTap(source, dest, Material,
            new Vector2(-offset, -offset),
            new Vector2(-offset, offset),
            new Vector2(offset, offset),
            new Vector2(offset, -offset));
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (Material == null)
        {
            Graphics.Blit(source, destination);
            return;
        }
        int width = Mathf.Max(1, source.width / 4);
        int height = Mathf.Max(1, source.height / 4);
        RenderTexture buffer = RenderTexture.GetTemporary(width, height, 0);
        DownSample4x(source, buffer);
        for (int i = 0; i < iterations; i++)
        {
            RenderTexture next = RenderTexture.GetTemporary(width, height, 0);
            FourTapCone(buffer, next, i);
            RenderTexture.ReleaseTemporary(buffer);
            buffer = next;
        }
        Graphics.Blit(buffer, destination);
        RenderTexture.ReleaseTemporary(buffer);
    }

    private void OnDisable()
    {
        if (_material != null)
        {
            if (Application.isPlaying)
                Destroy(_material);
            else
                DestroyImmediate(_material);
            _material = null;
        }
    }
}
