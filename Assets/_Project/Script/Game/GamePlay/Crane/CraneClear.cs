using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(BoxCollider))]
public sealed class CraneClear : MonoBehaviour
{
    private readonly Dictionary<Collider, PixelBlock> _blockCache = new Dictionary<Collider, PixelBlock>(128);
    private readonly HashSet<PixelBlock> _clearedBlocks = new HashSet<PixelBlock>();

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnValidate()
    {
        BoxCollider clearCollider = GetComponent<BoxCollider>();
        if (clearCollider != null)
            clearCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        Clear(other);
    }

    private void OnTriggerStay(Collider other)
    {
        Clear(other);
    }

    private void OnTriggerExit(Collider other)
    {
        _blockCache.Remove(other);
    }

    private void OnDisable()
    {
        _blockCache.Clear();
        _clearedBlocks.Clear();
    }

    private void Clear(Collider other)
    {
        PixelBlock block = GetBlock(other);
        if (block != null && _clearedBlocks.Add(block))
            Destroy(block.gameObject);
    }

    private PixelBlock GetBlock(Collider other)
    {
        if (_blockCache.TryGetValue(other, out PixelBlock block))
            return block;

        block = other.GetComponentInParent<PixelBlock>();
        _blockCache.Add(other, block);
        return block;
    }
}
