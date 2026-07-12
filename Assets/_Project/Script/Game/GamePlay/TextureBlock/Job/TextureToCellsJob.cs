using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

[BurstCompile]
internal struct TextureToCellsJob : IJobParallelFor
{
    // Pixel nguồn và output cell: màu hiển thị cùng cờ solid 0/1.
    [ReadOnly] public NativeArray<Color32> TexturePixels;
    [WriteOnly] public NativeArray<Color32> CellColors;
    [WriteOnly] public NativeArray<byte> CellSolid;
    public int TextureWidth;
    public int TextureHeight;
    public int GridWidth;
    public int SampleStep;
    public byte AlphaLimit;

    /// <summary>Đổi một index cell sang pixel source, áp alpha threshold và ghi dữ liệu lưới.</summary>
    public void Execute(int index)
    {
        int cellX = index % GridWidth;
        int cellY = index / GridWidth;
        int sourceX = cellX * SampleStep;
        int sourceY = cellY * SampleStep;
        if (sourceX >= TextureWidth) sourceX = TextureWidth - 1;
        if (sourceY >= TextureHeight) sourceY = TextureHeight - 1;

        Color32 color = TexturePixels[sourceY * TextureWidth + sourceX];
        bool solid = color.a > AlphaLimit;
        if (solid) color.a = 255;

        CellColors[index] = color;
        CellSolid[index] = solid ? (byte)1 : (byte)0;
    }
}
