using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class PixelBlock : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField] private float _mass = 0.05f;
    [SerializeField] private float _linearDamping = 2.5f;
    [SerializeField] private float _angularDamping = 4f;
    [SerializeField] private float _sleepThreshold = 0.08f;

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
        ConfigureRigidbody();
        _rigidbody.isKinematic = false;
        _rigidbody.useGravity = true;
        _rigidbody.WakeUp();
    }

    public void ApplySawCompression(Vector3 pressDirection, float pressSpeed, Vector3 contactPoint, float force, float sideDamping, float maxVelocity)
    {
        if (!_released || force <= 0f || pressSpeed <= 0f)
            return;

        CacheComponents();
        _rigidbody.AddForceAtPosition(pressDirection * (pressSpeed * force), contactPoint, ForceMode.Acceleration);

        Vector3 velocity = _rigidbody.linearVelocity;
        Vector3 pressVelocity = pressDirection * Vector3.Dot(velocity, pressDirection);
        Vector3 sideVelocity = velocity - pressVelocity;
        _rigidbody.linearVelocity = Vector3.ClampMagnitude(pressVelocity + sideVelocity * sideDamping, maxVelocity);
    }

    private void Freeze()
    {
        _released = false;
        ConfigureRigidbody();
        _rigidbody.isKinematic = true;
        _rigidbody.useGravity = false;
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
    }

    private void ConfigureRigidbody()
    {
        _rigidbody.mass = _mass;
        _rigidbody.linearDamping = _linearDamping;
        _rigidbody.angularDamping = _angularDamping;
        _rigidbody.sleepThreshold = _sleepThreshold;
        _rigidbody.interpolation = RigidbodyInterpolation.None;
        _rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
        _rigidbody.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
        _rigidbody.solverIterations = 3;
        _rigidbody.solverVelocityIterations = 1;
    }

    private void CacheComponents()
    {
        if (_rigidbody == null)
            _rigidbody = GetComponent<Rigidbody>();

        if (_renderer == null)
            _renderer = GetComponentInChildren<Renderer>();
    }
}
