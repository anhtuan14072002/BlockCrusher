using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(BoxCollider))]
public sealed class ConveyorCrane : MonoBehaviour
{
    [SerializeField] private Vector3 _worldDirection = Vector3.right;
    [SerializeField] private float _speed = 1.5f;
    [SerializeField] private float _acceleration = 12f;

    private Vector3 _direction;
    private readonly Dictionary<Collider, PixelBlock> _blockCache = new Dictionary<Collider, PixelBlock>(128);
    private readonly Dictionary<PixelBlock, Rigidbody> _rigidbodyCache = new Dictionary<PixelBlock, Rigidbody>(128);

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
        PixelBlock block = GetBlock(other);
        if (block == null)
            return;

        block.Release();

        Rigidbody blockRigidbody = GetRigidbody(block);
        Vector3 velocity = blockRigidbody.linearVelocity;
        float currentSpeed = Vector3.Dot(velocity, _direction);
        float nextSpeed = Mathf.MoveTowards(currentSpeed, _speed, _acceleration * Time.fixedDeltaTime);
        blockRigidbody.linearVelocity = velocity + _direction * (nextSpeed - currentSpeed);
    }

    private void OnTriggerExit(Collider other)
    {
        if (_blockCache.TryGetValue(other, out PixelBlock block))
            _rigidbodyCache.Remove(block);

        _blockCache.Remove(other);
    }

    private void OnDisable()
    {
        _blockCache.Clear();
        _rigidbodyCache.Clear();
    }

    private void CacheDirection()
    {
        _direction = _worldDirection.sqrMagnitude > 0.0001f ? _worldDirection.normalized : Vector3.right;
    }

    private PixelBlock GetBlock(Collider other)
    {
        if (_blockCache.TryGetValue(other, out PixelBlock block))
            return block;

        block = other.GetComponentInParent<PixelBlock>();
        _blockCache.Add(other, block);
        return block;
    }

    private Rigidbody GetRigidbody(PixelBlock block)
    {
        if (_rigidbodyCache.TryGetValue(block, out Rigidbody blockRigidbody))
            return blockRigidbody;

        blockRigidbody = block.GetComponent<Rigidbody>();
        _rigidbodyCache.Add(block, blockRigidbody);
        return blockRigidbody;
    }
}
