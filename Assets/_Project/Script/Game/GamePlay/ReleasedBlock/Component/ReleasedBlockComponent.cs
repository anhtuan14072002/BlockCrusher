using Unity.Entities;
using Unity.Mathematics;

public struct ReleasedBlockComponent : IComponentData
{
    public const byte SolidConstraintDuration = 8;

    public int OwnerId;
    public float4 Color;
    public float LockedZ;
    public float MaxPlanarSpeed;
    public byte SuctionPathIndex;
    public byte SolidConstraintFrames;
    public byte RenderAsMetaball;
}

public struct SuctionTransit : IComponentData, IEnableableComponent
{
}

public struct ReleasedBlockSolidConstraint : IComponentData, IEnableableComponent
{
}
