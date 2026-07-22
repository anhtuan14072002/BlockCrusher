using Unity.Entities;

using Unity.Mathematics;
public struct ReleasedBlockComponent : IComponentData
{
    public int OwnerId;
    public float4 Color;
    public float LockedZ;
    public float MaxPlanarSpeed;
    public byte SuctionPathIndex;
}

public struct SuctionTransit : IComponentData, IEnableableComponent
{
}
