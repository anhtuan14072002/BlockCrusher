using Unity.CharacterController;
using Unity.Entities;
using Unity.Physics.Systems;

namespace Wizard
{
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    [UpdateBefore(typeof(KinematicCharacterPhysicsUpdateGroup))]
    public partial struct BallMoveSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var ball in SystemAPI.Query<RefRW<KinematicCharacterBody>>())
            {
                ball.ValueRW.RelativeVelocity.z = 0f;
            }
        }
    }
}