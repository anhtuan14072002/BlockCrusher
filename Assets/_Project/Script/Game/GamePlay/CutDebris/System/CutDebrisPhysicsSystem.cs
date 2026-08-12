using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
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
                .WithAllRW<CutDebrisComponent>()
                .WithAll<Simulate>()
                .WithDisabled<CutDebrisSuctionTransit>()
                .Build();
            state.RequireForUpdate(_query);
        }

        public void OnUpdate(ref SystemState state)
        {
            JobHandle dependency = state.Dependency;
            for (int i = LevelMapAuthoring.ActiveSpawnerCount - 1; i >= 0; i--)
            {
                LevelMapAuthoring spawner = LevelMapAuthoring.GetActiveSpawner(i);
                if (spawner == null)
                {
                    LevelMapAuthoring.RemoveActiveSpawnerAt(i);
                    continue;
                }

                if (spawner.TryCreateCutDebrisMapGuardJob(
                        out LevelMapAuthoring.CutDebrisMapGuardJob mapGuardJob))
                    dependency = mapGuardJob.ScheduleParallel(_query, dependency);
            }

            state.Dependency = new ConstrainJob().ScheduleParallel(_query, dependency);
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
