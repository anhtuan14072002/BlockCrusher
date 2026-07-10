using Trove.Tweens;
using Unity.Burst;
using Unity.CharacterController;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Stateful;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

namespace Wizard
{
    [BurstCompile]
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    [UpdateBefore(typeof(KinematicCharacterPhysicsUpdateGroup))]
    public partial struct MiniTweenPadRandomSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteAllTrackedJobs();
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var padTween in SystemAPI.Query<RefRW<MiniTweenPadRandomComponent>>())
            {
                ref MiniTweenPadRandomComponent padRandom = ref padTween.ValueRW;

                if (!padRandom.isInitialized)
                {
                    padRandom.PadTweenInitialTransform = SystemAPI.GetComponent<LocalTransform>(padRandom.PadTween);
                    var padScale = padRandom.PadTweenInitialTransform;

                    ecb.AddComponent(padRandom.PadTween, new LocalScaleTween(
                        new TweenerFloat(padScale.Scale, padRandom.targetScale, false, EasingType.EaseInSine),
                        new TweenTimer(0.5f, false, false)
                    ));
                    ecb.AddComponent(padRandom.PadTween, new NonUniformScaleTween(
                        new TweenerFloat3(padRandom.initializedUniformScale, padRandom.targetUniformScale, false,
                            EasingType.EaseInOutBack),
                        new TweenTimer(0.75f, false, false)
                    ));

                    padRandom.isInitialized = true;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            ComponentLookup<LocalScaleTween> localScaleTweenLookup =
                SystemAPI.GetComponentLookup<LocalScaleTween>(false);
            ComponentLookup<NonUniformScaleTween> nonUniformScaleTween =
                SystemAPI.GetComponentLookup<NonUniformScaleTween>(false);
            foreach (var (randomPad, triggerEventsBuffer, entity) in
                     SystemAPI.Query<MiniTweenPadRandomComponent, DynamicBuffer<StatefulTriggerEvent>>()
                         .WithEntityAccess())
            {
                for (int i = 0; i < triggerEventsBuffer.Length; i++)
                {
                    StatefulTriggerEvent triggerEvent = triggerEventsBuffer[i];
                    if (triggerEvent.State != StatefulEventState.Enter) continue;
                    foreach (var (tweenRandom, entityRa) in SystemAPI.Query<RefRW<MiniTweenPadRandomComponent>>()
                                 .WithEntityAccess())
                    {
                        ref MiniTweenPadRandomComponent padRandom = ref tweenRandom.ValueRW;
                        ref LocalScaleTween t = ref localScaleTweenLookup.GetRefRW(padRandom.PadTween).ValueRW;
                        ref NonUniformScaleTween s = ref nonUniformScaleTween.GetRefRW(padRandom.PadTween).ValueRW;
                        t.Timer.SetCourse(true);
                        t.Timer.Play(false);
                        s.Timer.SetCourse(true);
                        s.Timer.Play(false);
                    }
                }
            }
        }
    }
}