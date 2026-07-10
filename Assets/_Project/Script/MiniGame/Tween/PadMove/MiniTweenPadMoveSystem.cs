using Trove.Tweens;
using Unity.Burst;
using Unity.CharacterController;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace Wizard
{
    [BurstCompile]
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup),OrderFirst = true)]
    [UpdateBefore(typeof(KinematicCharacterPhysicsUpdateGroup))]
    public partial struct MiniTweenPadMoveSystem : ISystem
    {
        [BurstCompile]
        void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (tweenPad, entity) in
                     SystemAPI.Query<RefRW<MiniTweenPadMoveComponent>>().WithEntityAccess())
            {
                ref MiniTweenPadMoveComponent padMove = ref tweenPad.ValueRW;
                if (padMove.isStop)
                {
                    if (SystemAPI.HasComponent<LocalPositionTween>(padMove.PadTween))
                    {
                        ecb.RemoveComponent<LocalPositionTween>(padMove.PadTween);
                    }
                    continue;
                }
                if (!padMove.isInitialized)
                {
                    padMove.PadTweenInitialTransform = SystemAPI.GetComponent<LocalTransform>(padMove.PadTween);

                    ecb.AddComponent(padMove.PadTween, new LocalPositionTween(
                        new TweenerFloat3(padMove.PadTweenInitialTransform.Position, new float3(tweenPad.ValueRO.targetPosition,0,0), true,
                            EasingType.EaseInOutQuad),
                        new TweenTimer(3f, true, true, 1f, true)));
                }
                padMove.isInitialized = true;
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}