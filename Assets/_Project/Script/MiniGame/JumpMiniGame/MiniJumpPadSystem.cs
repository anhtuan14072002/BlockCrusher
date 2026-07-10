using Unity.Burst;
using Unity.CharacterController;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Stateful;
using Unity.Physics.Systems;
using Unity.Physics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Wizard
{
    [BurstCompile]
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    [UpdateBefore(typeof(KinematicCharacterPhysicsUpdateGroup))]
    public partial struct MiniJumpPadSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            foreach (var (jumpPad, triggerEventsBuffer, entity) in SystemAPI
                         .Query<MiniJumpPadComponent, DynamicBuffer<StatefulTriggerEvent>>().WithEntityAccess())
            {
                for (int i = 0; i < triggerEventsBuffer.Length; i++)
                {
                    StatefulTriggerEvent triggerEvent = triggerEventsBuffer[i];
                    Entity otherEntity = triggerEvent.GetOtherEntity(entity);

                    if (triggerEvent.State != StatefulEventState.Enter) continue;

                    if (!SystemAPI.HasBuffer<MiniCheckJumpComponent>(otherEntity))
                    {
                        ecb.AddBuffer<MiniCheckJumpComponent>(otherEntity);
                    }

                    var jumpBuffer = SystemAPI.GetBuffer<MiniCheckJumpComponent>(otherEntity);

                    bool alreadyJumped = false;
                    for (int j = 0; j < jumpBuffer.Length; j++)
                    {
                        if (jumpBuffer[j].PadId == jumpPad.PadId)
                        {
                            alreadyJumped = true;
                            break;
                        }
                    }

                    if (alreadyJumped) continue;
                    Random random = new Random((uint)(SystemAPI.Time.ElapsedTime * 100000) + (uint)entity.Index);
                    int randomX = random.NextInt(jumpPad.JumpForceXMin, jumpPad.JumpForceXMax);
                    float3 jumForce = new float3(-randomX, jumpPad.JumpForce.y, jumpPad.JumpForce.z);

                    var velocity = SystemAPI.GetComponent<PhysicsVelocity>(otherEntity);
                    velocity.Linear = jumForce;
                    ecb.SetComponent(otherEntity, velocity);

                    var collider = SystemAPI.GetComponent<PhysicsCollider>(otherEntity);
                    if (SystemAPI.HasComponent<PhysicsCollider>(otherEntity))
                    {
                        collider.Value.Value.SetCollisionResponse(CollisionResponsePolicy.None);
                        ecb.SetComponent(otherEntity, collider);
                    }

                    jumpBuffer.Add(new MiniCheckJumpComponent { PadId = jumpPad.PadId });
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}