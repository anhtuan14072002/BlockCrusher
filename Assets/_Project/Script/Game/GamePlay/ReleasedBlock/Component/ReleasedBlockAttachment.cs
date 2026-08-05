using Crusher;
using Unity.Entities;
using Unity.Mathematics;

[InternalBufferCapacity(1)]
public struct ReleasedBlockAttachment : IBufferElementData
{
    public ushort TypeIndex;
    public TypeBlock CollectibleType;
    public float3 LocalPosition;
    public quaternion LocalRotation;
    public float3 Scale;
    public float4 Color;
}
