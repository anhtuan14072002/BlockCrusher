using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Stateful;
using Unity.Physics.Systems;
using Unity.CharacterController;
using Unity.Transforms;

namespace Wizard
{
    [BurstCompile]
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    [UpdateBefore(typeof(KinematicCharacterPhysicsUpdateGroup))]
    public partial struct MiniMultiSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MiniStoneComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.TempJob);
            MiniStoneComponent miniStoneComponent = SystemAPI.GetSingleton<MiniStoneComponent>();
            ComponentLookup<LocalPositionTween> localPositionTweenLookup = SystemAPI.GetComponentLookup<LocalPositionTween>(false);

            foreach (var (multi, triggerEventsBuffer, entity) in SystemAPI
                         .Query<MiniMultiComponent, DynamicBuffer<StatefulTriggerEvent>>()
                         .WithEntityAccess())
            {
                for (int i = 0; i < triggerEventsBuffer.Length; i++)
                {
                    StatefulTriggerEvent triggerEvent = triggerEventsBuffer[i];
                    Entity otherEntity = triggerEvent.GetOtherEntity(entity);

                    if (triggerEvent.State != StatefulEventState.Enter) continue;


                    float3 hitPosition = SystemAPI.GetComponent<LocalTransform>(otherEntity).Position;
                    if (!SystemAPI.HasBuffer<MiniCheckMultiComponent>(otherEntity))
                    {
                        state.EntityManager.AddBuffer<MiniCheckMultiComponent>(otherEntity);
                    }

                    var jumpBuffer = SystemAPI.GetBuffer<MiniCheckJumpComponent>(otherEntity);
                    var ballBuffer = SystemAPI.GetBuffer<MiniCheckMultiComponent>(otherEntity);
                    bool alreadyMulti = false;

                    for (int j = 0; j < ballBuffer.Length; j++)
                    {
                        if (ballBuffer[j].PadId != multi.PadId) continue;
                        alreadyMulti = true;
                        break;
                    }

                    if (alreadyMulti) continue;
                    for (int j = 1; j < multi.MultiNumber; j++)
                    {
                        float angle = (2 * math.PI / multi.MultiNumber) * j;
                        float xOffset = math.cos(angle) * multi.Radius;
                        float yOffset = math.sin(angle) * multi.Radius;

                        float3 positionMulti = hitPosition + new float3(xOffset, yOffset, 0);

                        Entity multiStone = ecb.Instantiate(miniStoneComponent.EntityStone);
                        ecb.SetComponent(multiStone, new LocalTransform
                        {
                            Position = positionMulti,
                            Rotation = quaternion.identity,
                            Scale = 0.3f,
                        });
                        var multiCheckBuffer = ecb.AddBuffer<MiniCheckMultiComponent>(multiStone);
                        multiCheckBuffer.Add(new MiniCheckMultiComponent { PadId = multi.PadId });

                        var newJumpBuffer = ecb.AddBuffer<MiniCheckJumpComponent>(multiStone);
                        for (int k = 0; k < jumpBuffer.Length; k++)
                        {
                            newJumpBuffer.Add(jumpBuffer[k]);
                        }
                    }

                    if (multi.PadId == multi.PadIdMove)
                    {
                        foreach (var (tweenPadMove, entityTween) 
                                 in SystemAPI.Query<RefRW<MiniTweenPadMoveComponent>>().WithEntityAccess())
                        {
                            tweenPadMove.ValueRW.isStop = true;

                            if (localPositionTweenLookup.HasComponent(tweenPadMove.ValueRW.PadTween))
                            {
                                ref LocalPositionTween t = ref localPositionTweenLookup.GetRefRW(tweenPadMove.ValueRW.PadTween).ValueRW;
                                t.Timer.SetCourse(false);
                                t.Timer.Play(false);
                            }
                        }
                    }
                }
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}          