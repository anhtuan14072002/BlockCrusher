using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

internal sealed class RenderFrameData
{
    // Native output của render job: transform, màu, số instance, dependency và trạng thái frame.
    public NativeArray<float4x4> Matrices;
    public NativeArray<float4> Colors;
    public NativeReference<int> Count;
    public JobHandle Handle;
    public bool Pending;

    /// <summary>Cấp phát buffer persistent đủ chứa tối đa debris của spawner.</summary>
    public RenderFrameData(int capacity)
    {
        Matrices = new NativeArray<float4x4>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        Colors = new NativeArray<float4>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        Count = new NativeReference<int>(Allocator.Persistent);
    }

    /// <summary>Hoàn trả toàn bộ native allocation của frame.</summary>
    public void Dispose()
    {
        if (Matrices.IsCreated) Matrices.Dispose();
        if (Colors.IsCreated) Colors.Dispose();
        if (Count.IsCreated) Count.Dispose();
    }
}
