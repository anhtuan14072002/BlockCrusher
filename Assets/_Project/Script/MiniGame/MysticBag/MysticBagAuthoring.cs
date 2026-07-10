using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Stateful;
using UnityEngine;
using UnityEngine.Serialization;

namespace Wizard
{
    public class MysticBagAuthoring : MonoBehaviour
    {
        public int _initialStones;

        class Baker : Baker<MysticBagAuthoring>
        {
            public override void Bake(MysticBagAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new MysticBagComponent
                    {
                        MysticStone = authoring._initialStones
                    }
                );
            }
        }
    }
}