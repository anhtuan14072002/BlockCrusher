using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

internal sealed class RenderFrameData
{
    public NativeArray<float4x4> Matrices;
    public NativeArray<float4> Colors;
    public NativeReference<int> Count;
    public JobHandle Handle;
    public bool Pending;

    public RenderFrameData(int capacity)
    {
        Matrices = new NativeArray<float4x4>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        Colors = new NativeArray<float4>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        Count = new NativeReference<int>(Allocator.Persistent);
    }

    public void Dispose()
    {
        if (Matrices.IsCreated) Matrices.Dispose();
        if (Colors.IsCreated) Colors.Dispose();
        if (Count.IsCreated) Count.Dispose();
    }
}
