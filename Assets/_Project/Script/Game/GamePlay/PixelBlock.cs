using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class PixelBlock : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private Rigidbody _rigidbody;
    private Renderer _renderer;
    private MaterialPropertyBlock _propertyBlock;
    private bool _released;

    private void Awake()
    {
        CacheComponents();
        Freeze();
    }

    public void Initialize(Color32 color, bool applyColor)
    {
        CacheComponents();
        Freeze();

        if (!applyColor || _renderer == null)
            return;

        _propertyBlock ??= new MaterialPropertyBlock();
        _renderer.GetPropertyBlock(_propertyBlock);
        Color linearColor = color;
        _propertyBlock.SetColor(BaseColorId, linearColor);
        _propertyBlock.SetColor(ColorId, linearColor);
        _renderer.SetPropertyBlock(_propertyBlock);
    }

    public void Release()
    {
        if (_released)
            return;

        CacheComponents();
        _released = true;
        _rigidbody.isKinematic = false;
        _rigidbody.useGravity = true;
        _rigidbody.WakeUp();
    }

    public void ApplySawMotion(Vector3 velocity, Vector3 contactPoint, float force)
    {
        if (!_released || force <= 0f)
            return;

        CacheComponents();
        _rigidbody.AddForceAtPosition(velocity * force, contactPoint, ForceMode.Acceleration);
    }

    private void Freeze()
    {
        _released = false;
        _rigidbody.isKinematic = true;
        _rigidbody.useGravity = false;
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
    }

    private void CacheComponents()
    {
        if (_rigidbody == null)
            _rigidbody = GetComponent<Rigidbody>();

        if (_renderer == null)
            _renderer = GetComponentInChildren<Renderer>();
    }
}
