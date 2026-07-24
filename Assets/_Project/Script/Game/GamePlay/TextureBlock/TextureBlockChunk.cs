using UnityEngine;

public sealed class TextureBlockChunk : MonoBehaviour
{
    private TextureBlockSpawner _spawner;

    public void Initialize(TextureBlockSpawner spawner)
    {
        _spawner = spawner;
    }

    public bool ReleaseAtWorld(Vector3 worldPoint, Vector3 pressDirection, float pressSpeed, float outwardForce,
        float tangentialForce, float spinDirection, float bladeRadius, float sideDamping, float maxVelocity)
    {
        return _spawner != null && _spawner.ReleaseAtWorld(worldPoint, pressDirection, pressSpeed, outwardForce,
            tangentialForce, spinDirection, bladeRadius, sideDamping, maxVelocity);
    }
}
