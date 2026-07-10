/*using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Serialization;
using VInspector;

namespace Wizard
{
    public class MapEdit : MonoBehaviour
    {
        public int idMap;

        [Button("Edit Map")]
        public void EditMap()
        {
            Debug.Log(123);
            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            EntityQuery entityQuery = entityManager.CreateEntityQuery(typeof(MapChangeComponent));
            NativeArray<MapChangeComponent> mapComponents =
                entityQuery.ToComponentDataArray<MapChangeComponent>(Allocator.Temp);
            Debug.Log(mapComponents.Length);
            for (int i = 0; i < mapComponents.Length; i++)
            {
                MapChangeComponent mapComponent = mapComponents[i];
                mapComponent.Id = idMap;
                mapComponents[i] = mapComponent;
               
            }
            entityQuery.CopyFromComponentDataArray(mapComponents);
            mapComponents.Dispose();
        }
    }
}*/