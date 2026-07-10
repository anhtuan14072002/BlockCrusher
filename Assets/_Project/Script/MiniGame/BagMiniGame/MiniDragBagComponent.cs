using Unity.Entities;
using Unity.Mathematics;

namespace Wizard
{
    public struct MiniDragBagComponent : IComponentData
    {
        public bool IsDrag;
        public bool IsDragging;
        public float DragTime;
        public bool IsCreate;
        public float3 PositionDragBag;
    }
}