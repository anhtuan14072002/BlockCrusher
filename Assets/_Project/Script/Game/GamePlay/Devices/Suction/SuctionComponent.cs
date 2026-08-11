using Unity.Entities;
using Unity.Mathematics;

namespace Crusher
{
    public struct SuctionComponent : IComponentData
    {
        public float Speed;
        public float TargetScale;
        public float ShrinkSpeed;
        public byte Active;
    }

    [InternalBufferCapacity(24)]
    public struct SuctionPathPoint : IBufferElementData
    {
        public float3 Value;
    }
}
