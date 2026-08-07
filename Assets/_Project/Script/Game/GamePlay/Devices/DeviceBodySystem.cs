using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace Crusher
{
    [UpdateInGroup(typeof(BeforePhysicsSystemGroup), OrderFirst = true)]
    public partial class DeviceBodySystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float stepFrequency = SystemAPI.Time.DeltaTime > 0f ? 1f / SystemAPI.Time.DeltaTime : 0f;
            foreach (var (deviceReference, transformReference, velocityReference, massReference) in
                     SystemAPI.Query<RefRW<DeviceBodyComponent>, RefRW<LocalTransform>,
                         RefRW<PhysicsVelocity>, RefRO<PhysicsMass>>())
            {
                ref DeviceBodyComponent device = ref deviceReference.ValueRW;
                ref LocalTransform transform = ref transformReference.ValueRW;
                ref PhysicsVelocity velocity = ref velocityReference.ValueRW;
                if (device.Initialized == 0)
                {
                    device.TargetPosition = transform.Position;
                    device.TargetRotation = transform.Rotation;
                    device.Initialized = 1;
                }

                if (device.Active == 0)
                {
                    velocity = PhysicsVelocity.Zero;
                    continue;
                }

                velocity = PhysicsVelocity.CalculateVelocityToTarget(
                    massReference.ValueRO, transform.Position, transform.Rotation,
                    new RigidTransform(device.TargetRotation, device.TargetPosition), stepFrequency);
            }
        }
    }
}
