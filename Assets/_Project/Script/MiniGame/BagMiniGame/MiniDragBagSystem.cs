using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Wizard
{
    public partial struct MiniDragBagSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            float minX = -2f;
            float maxX = 2f;
            bool isDragging = Input.GetMouseButton(0);

            foreach (var (localTransform, dragBag)
                     in SystemAPI.Query<RefRW<LocalTransform>, RefRW<MiniDragBagComponent>>()
                         .WithAll<MiniBagComponent>())
            {
                if (!dragBag.ValueRO.IsDrag)
                {
                    dragBag.ValueRW.IsDragging = false;
                    continue;
                }

                dragBag.ValueRW.IsDragging = isDragging;
                if (!isDragging) continue;

                var mousePos = Input.mousePosition;
                mousePos.z = Camera.main.WorldToScreenPoint(localTransform.ValueRO.Position).z;
                var worldPos = Camera.main.ScreenToWorldPoint(mousePos);

                var clampedX = math.clamp(worldPos.x, minX, maxX);
                localTransform.ValueRW.Position = new float3(clampedX, localTransform.ValueRO.Position.y,
                    localTransform.ValueRO.Position.z);
            }
        }
    }
}
