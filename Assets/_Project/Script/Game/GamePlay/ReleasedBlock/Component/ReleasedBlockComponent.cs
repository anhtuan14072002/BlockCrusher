using Unity.Entities;
using Unity.Mathematics;

public struct ReleasedBlockComponent : IComponentData
{
    public int OwnerId;
    public float4 Color;
    public float LockedZ;
    public float MaxPlanarSpeed;
    public float Radius;
    public float3 PhysicsStepStartPosition;
    public ushort TypeIndex;
    public byte SuctionPathIndex;
    public byte RenderAsMetaball;
    public byte UsesGravity;
}

public struct SuctionTransit : IComponentData, IEnableableComponent
{
}

public struct ReleasedBlockSolidConstraint : IComponentData, IEnableableComponent
{
}
