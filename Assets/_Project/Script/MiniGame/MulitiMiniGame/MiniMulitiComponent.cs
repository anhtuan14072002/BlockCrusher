using Unity.Entities;

namespace Wizard
{
    public struct MiniMultiComponent : IComponentData
    {
        public int MultiNumber;
        public int PadId;
        public int PadIdMove;
        public float Radius;
    }
}