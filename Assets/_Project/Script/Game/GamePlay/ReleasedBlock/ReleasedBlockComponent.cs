using Unity.Entities;
using Unity.Mathematics;

public struct ReleasedBlockComponent : IComponentData
{
    public int OwnerId;
    public float4 Color;
}
