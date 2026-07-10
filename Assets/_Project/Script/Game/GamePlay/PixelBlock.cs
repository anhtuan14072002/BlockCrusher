using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class PixelBlock : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField] private float _mass = 0.02f;
    [SerializeField] private float _gravityMultiplier = 0.3f;
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

    private void FixedUpdate()
    {
        if (!_released || _gravityMultiplier <= 0f)
            return;

        _rigidbody.AddForce(Physics.gravity * _gravityMultiplier, ForceMode.Acceleration);
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
        _rigidbody.useGravity = false;
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

    internal void SetMass(float mass)
    {
        _mass = mass;
        CacheComponents();
        _rigidbody.mass = _mass;
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

        float pushScale = 1f + radiusPush * 1.25f;
        float targetOutwardSpeed = outwardForce * 0.08f * pushScale * speedScale;
        if (outwardSpeed < targetOutwardSpeed)
            velocity += outward * (targetOutwardSpeed - Mathf.Max(outwardSpeed, 0f));

        Vector3 digDirection = Vector3.up + tangent * 0.1f;
        digDirection.Normalize();

        float digSpeed = Vector3.Dot(velocity, digDirection);
        if (digSpeed < 0f)
            velocity -= digDirection * digSpeed;

        float bladeCarrySpeed = tangentialForce * 0.42f * pushScale * speedScale * radiusPush;
        if (digSpeed < bladeCarrySpeed)
            velocity += digDirection * (bladeCarrySpeed - Mathf.Max(digSpeed, 0f));

        velocity -= outward * Vector3.Dot(velocity, outward) * sideDamping * radiusPush * 0.15f;
        _rigidbody.linearVelocity = Vector3.ClampMagnitude(velocity, maxVelocity);

        float targetAngularSpeed = tangentialForce * Mathf.Sign(spinDirection) * radiusPush;
        Vector3 angularVelocity = _rigidbody.angularVelocity;
        if (Mathf.Abs(angularVelocity.z) < Mathf.Abs(targetAngularSpeed))
            angularVelocity.z = targetAngularSpeed;
        _rigidbody.angularVelocity = angularVelocity;
    }

    private void Freeze()
    {
        _released = false;
        ConfigureRigidbody();
        _rigidbody.useGravity = false;
        if (!_rigidbody.isKinematic)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }
        _rigidbody.isKinematic = true;
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
