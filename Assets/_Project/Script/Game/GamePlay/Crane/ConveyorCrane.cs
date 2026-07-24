using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class ConveyorCrane : MonoBehaviour
{
    [SerializeField] private Vector3 _worldDirection = Vector3.right;
    [SerializeField] private float _speed = 1.5f;
    [SerializeField] private float _acceleration = 12f;

    private Vector3 _direction;
    private BoxCollider _conveyorCollider;

    private void Awake()
    {
        CacheDirection();
        _conveyorCollider = GetComponent<BoxCollider>();
        _conveyorCollider.isTrigger = true;
    }

    private void OnValidate()
    {
        CacheDirection();
        Collider conveyorCollider = GetComponent<Collider>();
        if (conveyorCollider != null)
            conveyorCollider.isTrigger = true;
    }

    private void FixedUpdate()
    {
        if (_conveyorCollider != null)
            TextureBlockSpawner.ApplyConveyorForActiveSpawners(
                _conveyorCollider.bounds, _direction, _speed, _acceleration, Time.fixedDeltaTime); 
    }

    private void CacheDirection()
    {
        _direction = _worldDirection.sqrMagnitude > 0.0001f ? _worldDirection.normalized : Vector3.right;
    }
}
