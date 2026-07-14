using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Transforms;

[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
internal partial struct PrepareRenderFrameJob : IJobEntity
{
    [WriteOnly] public NativeArray<float4x4> Matrices;
    [WriteOnly] public NativeArray<float4> Colors;
    public NativeReference<int> Count;
    public int OwnerId;

    private void Execute(in LocalTransform transformData, in ReleasedBlockComponent block)
    {
        if (block.OwnerId != OwnerId)
            return;

        int index = Count.Value;
        if (index >= Matrices.Length)
            return;

        Matrices[index] = float4x4.TRS(transformData.Position, transformData.Rotation,
            new float3(transformData.Scale));
        Colors[index] = block.Color;
        Count.Value = index + 1;
    }
}

[UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
[UpdateAfter(typeof(ReleasedBlockPlanarConstraintSystem))]
public partial struct ReleasedBlockRenderPreparationSystem : ISystem
{
    private EntityQuery _query;

    public void OnCreate(ref SystemState state)
    {
        _query = SystemAPI.QueryBuilder()
            .WithAll<LocalTransform, ReleasedBlockComponent>()
            .Build();
    }

    public void OnUpdate(ref SystemState state)
    {
        JobHandle dependency = state.Dependency;
        for (int i = TextureBlockSpawner.ActiveSpawnerCount - 1; i >= 0; i--)
        {
            TextureBlockSpawner spawner = TextureBlockSpawner.GetActiveSpawner(i);
            if (spawner == null)
            {
                TextureBlockSpawner.RemoveActiveSpawnerAt(i);
                continue;
            }
            if (!spawner.TryCreateRenderPreparationJob(out PrepareRenderFrameJob job))
                continue;
            JobHandle handle = job.Schedule(_query, dependency);
            spawner.SetRenderPreparationHandle(handle);
            dependency = handle;
        }
        state.Dependency = dependency;
    }
}
