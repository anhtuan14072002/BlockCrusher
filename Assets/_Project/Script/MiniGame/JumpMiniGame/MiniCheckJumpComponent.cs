using Unity.Entities;

namespace Wizard
{
    [InternalBufferCapacity(3)]
    public struct MiniCheckJumpComponent : IBufferElementData
    {
        public int PadId;
        //public bool HasJumpedPad;
    }
}