using Unity.Entities;
using Unity.Mathematics;

namespace Wizard
{
    public struct MiniStoneComponent : IComponentData
    {
        public Entity EntityStone;
        public int Amount;
        public float ElapsedTime;
        
        public int MinMapFew;
        public int MaxMapFew;
  
        public int MinMapMedium;
        public int MaxMapMedium;
        
        public int MinMapMany;
        public int MaxMapMany;
        
    }
}