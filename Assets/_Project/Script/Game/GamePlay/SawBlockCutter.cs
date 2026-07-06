using UnityEngine;

public sealed class SawBlockCutter : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        Release(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        Release(collision.collider);
    }

    private static void Release(Collider other)
    {
        PixelBlock block = other.GetComponentInParent<PixelBlock>();
        if (block != null)
            block.Release();
    }
}
