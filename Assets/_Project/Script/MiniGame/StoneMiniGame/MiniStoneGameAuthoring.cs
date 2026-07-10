using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Stateful;
using UnityEngine;
using UnityEngine.Serialization;

namespace Wizard
{
    public class MiniStoneGameAuthoring : MonoBehaviour
    {
        [SerializeField] private GameObject stonePrefab;
        public int amount;
        public float elapsedTime;

        public int minMapFew;
        public int maxMapFew;
        public int minMapMedium;
        public int maxMapMedium;
        public int minMapMany;
        public int maxMapMany;

        public float ElapsedTime => elapsedTime;

        public bool TryConsumeStone(out GameObject prefab)
        {
            prefab = stonePrefab;
            if (amount <= 0 || stonePrefab == null) return false;

            amount--;
            return true;
        }
        
        class Baker : Baker<MiniStoneGameAuthoring>
        {
            public override void Bake(MiniStoneGameAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new MiniStoneComponent
                {
                    EntityStone = GetEntity(authoring.stonePrefab, TransformUsageFlags.Dynamic),
                    Amount = authoring.amount,
                    ElapsedTime = authoring.elapsedTime,
                    
                    MinMapFew = authoring.minMapFew,
                    MaxMapFew = authoring.maxMapFew,
                    MinMapMedium = authoring.minMapMedium,
                    MaxMapMedium = authoring.maxMapMedium,  
                    MinMapMany = authoring.minMapMany,
                    MaxMapMany = authoring.maxMapMany,
                    
                });
                AddBuffer<StatefulTriggerEvent>(entity);
            }
        }
    }
}
