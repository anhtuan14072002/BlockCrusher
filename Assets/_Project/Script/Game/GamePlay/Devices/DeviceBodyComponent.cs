using Unity.Entities;
using Unity.Mathematics;

namespace Crusher
{
    public struct DeviceBodyComponent : IComponentData
    {
        public float3 TargetPosition;
        public quaternion TargetRotation;
        public byte Active;
        public byte Initialized;
    }

    public struct SuctionDeviceTag : IComponentData
    {
    }
}
