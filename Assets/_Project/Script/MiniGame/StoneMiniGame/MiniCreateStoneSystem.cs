using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Wizard
{
    [BurstCompile]
    public partial struct MiniCreateStoneSystem : ISystem
    {
        private float _timer;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MiniStoneComponent>();
            state.RequireForUpdate<MiniDragBagComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var miniStoneComponent = SystemAPI.GetSingleton<MiniStoneComponent>();
            var miniDragBagComponent = SystemAPI.GetSingleton<MiniDragBagComponent>();
            if (!miniDragBagComponent.IsDragging) return;

            if (miniStoneComponent.Amount == 0) return;
            float elapsedTime = miniStoneComponent.ElapsedTime;
            _timer += SystemAPI.Time.DeltaTime;

            if (!(_timer >= elapsedTime)) return;
            _timer = 0f;

            Entity miniStoneEntity = SystemAPI.GetSingletonEntity<MiniStoneComponent>();
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            foreach (var localTransform in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<MiniBagComponent>())
            {
                Entity createStone = ecb.Instantiate(miniStoneComponent.EntityStone);
                ecb.SetComponent(createStone, LocalTransform.FromPositionRotationScale(
                    localTransform.ValueRO.Position, quaternion.identity, 0.3f));

                ecb.AddBuffer<MiniCheckJumpComponent>(createStone);
                ecb.AddBuffer<MiniCheckMultiComponent>(createStone);
                miniStoneComponent.Amount--;
                SystemAPI.SetComponent(miniStoneEntity, miniStoneComponent);
                if (miniStoneComponent.Amount == 0)
                    break;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
