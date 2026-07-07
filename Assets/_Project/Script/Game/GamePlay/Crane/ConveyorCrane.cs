using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class ConveyorCrane : MonoBehaviour
{
    [SerializeField] private Vector3 _worldDirection = Vector3.right;
    [SerializeField] private float _speed = 1.5f;
    [SerializeField] private float _acceleration = 12f;

    private Vector3 _direction;

    private void Awake()
    {
        CacheDirection();

        Collider conveyorCollider = GetComponent<Collider>();
        conveyorCollider.isTrigger = true;
    }

    private void OnValidate()
    {
        CacheDirection();

        Collider conveyorCollider = GetComponent<Collider>();
        if (conveyorCollider != null)
            conveyorCollider.isTrigger = true;
    }

    private void OnTriggerStay(Collider other)
    {
        PixelBlock block = other.GetComponentInParent<PixelBlock>();
        if (block == null)
            return;

        block.Release();

        Rigidbody blockRigidbody = block.GetComponent<Rigidbody>();
        Vector3 velocity = blockRigidbody.linearVelocity;
        float currentSpeed = Vector3.Dot(velocity, _direction);
        float nextSpeed = Mathf.MoveTowards(currentSpeed, _speed, _acceleration * Time.fixedDeltaTime);
        blockRigidbody.linearVelocity = velocity + _direction * (nextSpeed - currentSpeed);
    }

    private void CacheDirection()
    {
        _direction = _worldDirection.sqrMagnitude > 0.0001f ? _worldDirection.normalized : Vector3.right;
    }
}
