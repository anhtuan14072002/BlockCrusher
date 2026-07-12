using Unity.Entities;
using Unity.Mathematics;

public struct ReleasedBlockComponent : IComponentData
{
    // Ngưỡng vận tốc và số tick cần đứng yên để debris được coi là đã ổn định.
    public const float SettleSpeed = 0.05f;
    public const byte SettleFrames = 6;

    // Owner dùng để mỗi TextureBlockSpawner chỉ điều khiển/render debris của chính nó.
    public int OwnerId;
    public float4 Color;
    public float LockedZ;
    public float MaxPlanarSpeed;
    public byte StableFrames;
}
