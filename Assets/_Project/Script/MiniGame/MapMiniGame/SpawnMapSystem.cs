using System;
using System.Collections.Generic;
using Core;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Wizard
{
    /*[UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(VariableRateSimulationSystemGroup))]*/
    public partial struct SpawnMapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MapBufferComponent>();
            state.RequireForUpdate<MiniStoneComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            /*// lấy id từ quantity
            // check số bóng, nếu từ 0 đến 3 trar về few, 4 đến 6 là medium, 7 đến 9 là many
            // sau đó random id của giá trị trả về rồi instantiate prefab id đó
            var map = SystemAPI.GetSingletonBuffer<MapBufferComponent>();
            var miniStoneComponent = SystemAPI.GetSingleton<MiniStoneComponent>();

            var currentStoneCount = miniStoneComponent.Amount;
            var minMapFew = miniStoneComponent.MinMapFew;
            var maxMapFew = miniStoneComponent.MaxMapFew;
            var minMapMedium = miniStoneComponent.MinMapMedium;
            var maxMapMedium = miniStoneComponent.MaxMapMedium;
            var minMapMany = miniStoneComponent.MinMapMany;
            var maxMapMany = miniStoneComponent.MaxMapMany;

            MapType mapType = CovertMapType(currentStoneCount, minMapFew, maxMapFew, minMapMedium, maxMapMedium,
                minMapMany, maxMapMany);
            Debug.Log(mapType);
            var mapMiniGameDict = Game.Config.MapMiniGameDict;
            var fewIds = new List<int>();
            var mediumIds = new List<int>();
            var manyIds = new List<int>();

            foreach (var mapIds in mapMiniGameDict)
            {
                var mapId = ParseMapType(mapIds.Value.Quantity);
                switch (mapId)
                {
                    case MapType.Few:
                        fewIds.Add(mapIds.Key);
                        break;
                    case MapType.Medium:
                        mediumIds.Add(mapIds.Key);
                        break;
                    case MapType.Many:
                        manyIds.Add(mapIds.Key);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            List<int> targetIds = mapType switch
            {
                MapType.Few => fewIds,
                MapType.Medium => mediumIds,
                MapType.Many => manyIds,
            };
            if (targetIds.Count > 0)
            {
                int randomIndex = Random.Range(0, targetIds.Count);
                int selectedId = targetIds[randomIndex];
                MapBufferComponent? selectedMap = null;
                foreach (var bufferElement in map)
                {
                    if (bufferElement.Id == selectedId)
                    {
                        selectedMap = bufferElement;
                        break;
                    }
                }

                state.EntityManager.Instantiate(selectedMap.Value.MapEntity);
            }

            state.Enabled = false;*/
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static MapType CovertMapType(int stoneCount, int minMapFew, int maxMapFew, int minMapMedium,
            int maxMapMedium, int minMapMany, int maxMapMany)
        {
            if (stoneCount >= minMapFew && stoneCount <= maxMapFew) return MapType.Few;
            if (stoneCount >= minMapMedium && stoneCount <= maxMapMedium) return MapType.Medium;
            if (stoneCount >= minMapMany && stoneCount <= maxMapMany) return MapType.Many;
            throw new Exception($"Unknown CovertMapType {stoneCount}");
        }

        private static MapType ParseMapType(string quantity)
        {
            return quantity switch
            {
                "Few" => MapType.Few,
                "Medium" => MapType.Medium,
                "Many" => MapType.Many,
                _ => throw new Exception($"Unknown quantity: {quantity}")
            };
        }
    }
    public enum MapType
    {
        Few = 1,
        Medium = 2,
        Many = 3
    }
}
/*
 * state.RequireForUpdate<MapBufferComponent>();
            var map = SystemAPI.GetSingletonBuffer<MapBufferComponent>();
            var indexMap = Random.Range(0, map.Length);
            var mapConfig = Game.Config.MapMiniGameDict[9003];
            Debug.Log(mapConfig.Quantity);
            state.EntityManager.Instantiate(map[indexMap].MapEntity);
            state.Enabled = false;
 */