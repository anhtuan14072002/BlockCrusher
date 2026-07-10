using Unity.Entities;
using Unity.Mathematics;

namespace Wizard
{
    public struct MiniBagComponent : IComponentData
    {
        public Entity EntityBag;
        public float3 PositionBag;
    }
}