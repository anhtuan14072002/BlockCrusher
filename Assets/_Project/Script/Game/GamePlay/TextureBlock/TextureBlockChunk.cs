using UnityEngine;

public sealed class TextureBlockChunk : MonoBehaviour
{
    // Owner thực hiện chuyển đổi world-space sang cell grid và spawn debris.
    private TextureBlockSpawner _spawner;

    /// <summary>Gắn chunk runtime với spawner đã tạo nó.</summary>
    public void Initialize(TextureBlockSpawner spawner)
    {
        _spawner = spawner;
    }

    /// <summary>Forward yêu cầu cắt từ collider chunk tới owner spawner.</summary>
    public bool ReleaseAtWorld(Vector3 worldPoint, Vector3 pressDirection, float pressSpeed, float outwardForce,
        float tangentialForce, float spinDirection, float bladeRadius, float sideDamping, float maxVelocity)
    {
        return _spawner != null && _spawner.ReleaseAtWorld(worldPoint, pressDirection, pressSpeed, outwardForce,
            tangentialForce, spinDirection, bladeRadius, sideDamping, maxVelocity);
    }
}
