using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
internal struct PrepareRenderFrameJob : IJob
{
    [ReadOnly] public NativeArray<LocalTransform> Transforms;
    [ReadOnly] public NativeArray<ReleasedBlockComponent> Blocks;
    [WriteOnly] public NativeArray<float4x4> Matrices;
    [WriteOnly] public NativeArray<float4> Colors;
    public NativeReference<int> Count;
    public int OwnerId;

    public void Execute()
    {
        int count = 0;
        int length = math.min(Transforms.Length, Blocks.Length);
        for (int i = 0; i < length && count < Matrices.Length; i++)
        {
            ReleasedBlockComponent block = Blocks[i];
            if (block.OwnerId != OwnerId) continue;

            LocalTransform transformData = Transforms[i];
            Matrices[count] = float4x4.TRS(transformData.Position, transformData.Rotation, new float3(transformData.Scale));
            Colors[count] = block.Color;
            count++;
        }

        Count.Value = count;
    }
}
