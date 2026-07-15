using Unity.Entities;

using Unity.Mathematics;
public struct ReleasedBlockComponent : IComponentData
{
    public const float SettleSpeed = 0.05f;
    public const byte SettleFrames = 6;
    public int OwnerId;
    public float4 Color;
    public float LockedZ;
    public float MaxPlanarSpeed;
    public byte StableFrames;
    public byte SuctionPathIndex;
}
