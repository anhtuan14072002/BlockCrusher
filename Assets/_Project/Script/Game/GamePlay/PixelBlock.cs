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
    private Transform _transform;
    private MaterialPropertyBlock _propertyBlock;
    private TextureBlockSpawner _poolOwner;
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

    public bool ReturnToPool()
    {
        if (_poolOwner == null)
            return false;

        _poolOwner.ReturnReleasedBlock(this);
        return true;
    }

    internal void SetPoolOwner(TextureBlockSpawner poolOwner)
    {
        _poolOwner = poolOwner;
    }

    internal void RecycleToPool()
    {
        CacheComponents();
        Freeze();
        gameObject.SetActive(false);
    }

    public void ApplySawCompression(Vector3 sawCenter, Vector3 sawMoveDirection, float pressSpeed, Vector3 contactPoint,
        float outwardForce, float tangentialForce, float spinDirection, float bladeRadius, float sideDamping,
        float maxVelocity)
    {
        if (!_released)
            return;

        Vector3 outward = _rigidbody.worldCenterOfMass - sawCenter;
        outward.z = 0f;
        if (outward.sqrMagnitude <= 0.0001f)
            outward = sawMoveDirection.sqrMagnitude > 0.0001f ? sawMoveDirection : Vector3.up;
        else
            outward.Normalize();

        Vector3 tangent = new Vector3(-outward.y, outward.x, 0f) * Mathf.Sign(spinDirection);
        float speedScale = 1f + Mathf.Min(pressSpeed, 4f) * 0.1f;
        float distanceFromBladeCenter = Vector3.Distance(new Vector3(_rigidbody.position.x, _rigidbody.position.y, 0f),
            new Vector3(sawCenter.x, sawCenter.y, 0f));
        float radiusPush = bladeRadius > 0f ? Mathf.Clamp01((bladeRadius - distanceFromBladeCenter) / bladeRadius) : 0f;

        Vector3 velocity = _rigidbody.linearVelocity;
        float outwardSpeed = Vector3.Dot(velocity, outward);
        if (outwardSpeed < 0f)
            velocity -= outward * outwardSpeed;

        float tangentSpeed = Vector3.Dot(velocity, tangent);
        velocity -= tangent * tangentSpeed * (radiusPush * (1f - sideDamping));

        float pushScale = 1f + radiusPush * 1.25f;
        float targetOutwardSpeed = outwardForce * 0.08f * pushScale * speedScale;
        if (outwardSpeed < targetOutwardSpeed)
            velocity += outward * (targetOutwardSpeed - Mathf.Max(outwardSpeed, 0f));

        velocity += tangent * (tangentialForce * 0.006f * pushScale);
        _rigidbody.linearVelocity = Vector3.ClampMagnitude(velocity, maxVelocity);
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
        _rigidbody.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX |
                                 RigidbodyConstraints.FreezeRotationY;
        _rigidbody.solverIterations = 3;
        _rigidbody.solverVelocityIterations = 1;
    }

    private void CacheComponents()
    {
        if (_transform == null)
            _transform = transform;

        if (_rigidbody == null)
            _rigidbody = GetComponent<Rigidbody>();

        if (_renderer == null)
            _renderer = GetComponentInChildren<Renderer>();
    }
}
