using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class CraneClear : MonoBehaviour
{
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

    private static void Clear(Collider other)
    {
        PixelBlock block = other.GetComponentInParent<PixelBlock>();
        if (block != null)
            Destroy(block.gameObject);
    }
}
