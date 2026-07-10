using Unity.Entities;

namespace Wizard
{
    [InternalBufferCapacity(3)]
    public struct MiniCheckMultiComponent : IBufferElementData
    {
        public int PadId;
    }
}