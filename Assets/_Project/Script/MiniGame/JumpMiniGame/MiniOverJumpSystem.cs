using Unity.Burst;
using Unity.CharacterController;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

namespace Wizard
{
    [BurstCompile]
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    [UpdateAfter(typeof(KinematicCharacterPhysicsUpdateGroup))]
    public partial struct MiniOverJumpSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (velocity, entity) in SystemAPI.Query<PhysicsVelocity>().WithEntityAccess())
            {
                if (velocity.Linear.y < 0)
                {
                    var collider = SystemAPI.GetComponent<PhysicsCollider>(entity);
                    collider.Value.Value.SetCollisionResponse(CollisionResponsePolicy.Collide);
                    ecb.SetComponent(entity, collider);
                }
            }
        }
    }
}