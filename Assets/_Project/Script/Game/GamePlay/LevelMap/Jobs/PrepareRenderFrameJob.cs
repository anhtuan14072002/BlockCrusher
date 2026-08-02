using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
internal partial struct PrepareRenderFrameJob : IJobEntity
{
    [WriteOnly] public NativeArray<float4x4> Matrices;
    [WriteOnly] public NativeArray<float4> Colors;
    [WriteOnly] public NativeArray<ushort> Types;
    public NativeReference<int> Count;
    public int OwnerId;

    private void Execute(Entity entity, in LocalTransform transformData, in ReleasedBlockComponent block)
    {
        if (block.OwnerId != OwnerId || block.RenderAsMetaball != 0)
            return;

        int index = Count.Value;
        if (index >= Matrices.Length)
            return;

        float3 renderPosition = transformData.Position;
        renderPosition.z += (entity.Index & 255) * 0.0001f;
        Matrices[index] = float4x4.TRS(renderPosition, transformData.Rotation,
            new float3(transformData.Scale));
        Colors[index] = block.Color;
        Types[index] = block.TypeIndex;
        Count.Value = index + 1;
    }
}

[UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
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
        for (int i = LevelMapSpawner.ActiveSpawnerCount - 1; i >= 0; i--)
        {
            LevelMapSpawner spawner = LevelMapSpawner.GetActiveSpawner(i);
            if (spawner == null)
            {
                LevelMapSpawner.RemoveActiveSpawnerAt(i);
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
