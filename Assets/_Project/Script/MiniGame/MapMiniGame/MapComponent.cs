using Unity.Entities;

namespace Wizard
{
    public struct MapComponent : IComponentData
    {
        public int Id;
        public int Quantity;
        public Entity MapEntity;
    }
    
}