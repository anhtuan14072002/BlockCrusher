using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
internal partial struct PrepareRenderFrameJob : IJobEntity
{
    private const float AttachmentRenderOffset = -0.005f;

    [WriteOnly] public NativeArray<float4x4> Matrices;
    [WriteOnly] public NativeArray<float4> Colors;
    [WriteOnly] public NativeArray<ushort> Types;
    public NativeReference<int> Count;
    public int OwnerId;

    private void Execute(Entity entity, in LocalTransform transformData, in ReleasedBlockComponent block,
        in DynamicBuffer<ReleasedBlockAttachment> attachments)
    {
        if (block.OwnerId != OwnerId || block.RenderAsMetaball != 0)
            return;

        int index = Count.Value;
        if (!TryWriteRecord(entity, index, block.TypeIndex, block.Color, transformData.Position,
                transformData.Rotation, new float3(transformData.Scale), 0f))
            return;

        Count.Value = index + 1;
        for (int i = 0; i < attachments.Length; i++)
        {
            ReleasedBlockAttachment attachment = attachments[i];
            index = Count.Value;
            float3 renderPosition = transformData.Position + math.rotate(transformData.Rotation, attachment.LocalPosition);
            quaternion renderRotation = math.mul(transformData.Rotation, attachment.LocalRotation);
            if (!TryWriteRecord(entity, index, attachment.TypeIndex, attachment.Color, renderPosition,
                    renderRotation, attachment.Scale, AttachmentRenderOffset))
                return;
            Count.Value = index + 1;
        }
    }

    private bool TryWriteRecord(Entity entity, int index, ushort typeIndex, float4 color, float3 renderPosition,
        quaternion renderRotation, float3 scale, float renderOffset)
    {
        if (index >= Matrices.Length)
            return false;

        renderPosition.z += (entity.Index & 255) * 0.0001f + renderOffset;
        Matrices[index] = float4x4.TRS(renderPosition, renderRotation, scale);
        Colors[index] = color;
        Types[index] = typeIndex;
        return true;
    }
}

[UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true)]
public partial struct ReleasedBlockRenderPreparationSystem : ISystem
{
    private EntityQuery _query;

    public void OnCreate(ref SystemState state)
    {
        _query = SystemAPI.QueryBuilder()
            .WithAll<LocalTransform, ReleasedBlockComponent, ReleasedBlockAttachment, Simulate>()
            .Build();
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
            if (!spawner.TryCreateRenderPreparationJob(out PrepareRenderFrameJob job))
                continue;
            JobHandle handle = job.Schedule(_query, dependency);
            spawner.SetRenderPreparationHandle(handle);
            dependency = handle;
        }
        state.Dependency = dependency;
    }
}
