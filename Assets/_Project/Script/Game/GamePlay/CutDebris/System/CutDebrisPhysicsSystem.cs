using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace Crusher
{
    [BurstCompile]
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    public partial struct CutDebrisPhysicsSystem : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAllRW<LocalTransform, PhysicsVelocity>()
                .WithAll<CutDebrisComponent, Simulate>()
                .WithDisabled<CutDebrisSuctionTransit>()
                .Build();
            state.RequireForUpdate(_query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new ConstrainJob().ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        private partial struct ConstrainJob : IJobEntity
        {
            private void Execute(ref LocalTransform transform, ref PhysicsVelocity velocity,
                in CutDebrisComponent debris)
            {
                transform.Position.z = debris.LockedZ;
                velocity.Linear.z = 0f;
                velocity.Angular.x = 0f;
                velocity.Angular.y = 0f;

                float speedSq = math.lengthsq(velocity.Linear.xy);
                float maxSpeedSq = debris.MaxPlanarSpeed * debris.MaxPlanarSpeed;
                if (speedSq <= maxSpeedSq)
                    return;

                float2 planarVelocity = math.normalize(velocity.Linear.xy) * debris.MaxPlanarSpeed;
                velocity.Linear.x = planarVelocity.x;
                velocity.Linear.y = planarVelocity.y;
            }
        }
    }
}
