using Core;
using Unity.Entities;
using UnityEngine;

namespace Wizard
{
    public class MapAuthoring : MonoBehaviour
    {
        public int mapId;

        class Baker : Baker<MapAuthoring>
        {
            public override void Bake(MapAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);
                LoadMapPrefab(entity);
            }

            private void LoadMapPrefab(Entity entity)
            {
                /*var mapBuffers = AddBuffer<MapBufferComponent>(entity);
                var dictIdMap = Game.Config.MapMiniGameDict;
                foreach (var map in dictIdMap)
                {
                    var mapPrefab = GameRes.LoadMapPrefabs(map.Key);
                    mapBuffers.Add(new MapBufferComponent()
                    {
                        Id = map.Key,
                        MapEntity = GetEntity(mapPrefab, TransformUsageFlags.None),
                    });
                }*/
            }
        }
    }
}