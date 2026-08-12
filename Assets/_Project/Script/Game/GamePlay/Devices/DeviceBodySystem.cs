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
            foreach (var (deviceReference, transformReference) in
                     SystemAPI.Query<RefRO<DeviceBodyComponent>, RefRW<LocalTransform>>()
                         .WithAll<SawDeviceTag>())
            {
                if (deviceReference.ValueRO.Initialized != 0)
                    transformReference.ValueRW.Rotation = deviceReference.ValueRO.TargetRotation;
            }

            float stepFrequency = SystemAPI.Time.DeltaTime > 0f ? 1f / SystemAPI.Time.DeltaTime : 0f;
            ComponentLookup<SawComponent> sawLookup = GetComponentLookup<SawComponent>(true);
            ComponentLookup<SawDeviceTag> sawTagLookup = GetComponentLookup<SawDeviceTag>(true);
            foreach (var (deviceReference, transformReference, velocityReference, massReference, entity) in
                     SystemAPI.Query<RefRW<DeviceBodyComponent>, RefRW<LocalTransform>,
                         RefRW<PhysicsVelocity>, RefRO<PhysicsMass>>().WithEntityAccess())
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

                if (!sawTagLookup.HasComponent(entity) || !sawLookup.HasComponent(entity))
                    continue;

                SawComponent saw = sawLookup[entity];
                if (saw.Active == 0 || math.abs(saw.BladeSpinAngularSpeed) <= 0.0001f)
                    continue;

                float3 worldSpinAxis = math.normalizesafe(
                    math.rotate(transform.Rotation, saw.BladeSpinAxis), new float3(0f, 0f, 1f));
                velocity.Angular += worldSpinAxis * saw.BladeSpinAngularSpeed;
            }
        }
    }
}
