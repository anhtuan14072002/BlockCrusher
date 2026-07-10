/*using Unity.CharacterController;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;
namespace Wizard
{
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    public partial struct MiniJumpOverSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.TempJob);

            foreach (var (stateJump, kineBody, entity) in SystemAPI
                         .Query<MiniJumpStateComponent, KinematicCharacterBody>().WithEntityAccess())
            {
                if (!stateJump.JumpState || kineBody.RelativeVelocity.y >= 0) continue;

                var collider = SystemAPI.GetComponent<PhysicsCollider>(entity);
                collider.Value.Value.SetCollisionFilter(new CollisionFilter
                {
                    BelongsTo = 1u << LayerMask.NameToLayer("Stone"),
                    CollidesWith = ~0u
                });
                SystemAPI.SetComponent(entity, collider);
                ecb.RemoveComponent<MiniJumpStateComponent>(entity);
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}*/