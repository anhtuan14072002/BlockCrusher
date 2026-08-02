using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class CraneClear : MonoBehaviour
{
    private BoxCollider _clearCollider;

    private void Awake()
    {
        _clearCollider = GetComponent<BoxCollider>();
        _clearCollider.isTrigger = true;
    }

    private void OnValidate()
    {
        BoxCollider clearCollider = GetComponent<BoxCollider>();
        if (clearCollider != null)
            clearCollider.isTrigger = true;
    }

    private void FixedUpdate()
    {
        if (_clearCollider != null)
            LevelMapSpawner.ClearReleasedBlocksForActiveSpawners(_clearCollider.bounds);
    }
}
