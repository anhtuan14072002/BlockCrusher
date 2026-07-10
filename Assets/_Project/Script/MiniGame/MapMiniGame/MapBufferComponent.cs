using Unity.Entities;

namespace Wizard
{
    [InternalBufferCapacity(64)]
    public struct MapBufferComponent : IBufferElementData
    {
        public int Id;
        /*
        public string Quantity;
        */
        public Entity MapEntity;
    }
}