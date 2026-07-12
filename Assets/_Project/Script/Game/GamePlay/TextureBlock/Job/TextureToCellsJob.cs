// Giải thích dòng gốc 1: Nạp namespace Unity.Burst để file dùng được các kiểu và API trong đó.
using Unity.Burst;
// Giải thích dòng gốc 2: Nạp namespace Unity.Collections để file dùng được các kiểu và API trong đó.
using Unity.Collections;
// Giải thích dòng gốc 3: Nạp namespace Unity.Jobs để file dùng được các kiểu và API trong đó.
using Unity.Jobs;
// Giải thích dòng gốc 4: Nạp namespace UnityEngine để file dùng được các kiểu và API trong đó.
using UnityEngine;

// Giải thích dòng gốc 6: Gắn attribute [BurstCompile] cho khai báo nằm ngay bên dưới.
[BurstCompile]
// Giải thích dòng gốc 7: Khai báo struct TextureToCellsJob là kiểu dữ liệu value type.
internal struct TextureToCellsJob : IJobParallelFor
// Giải thích dòng gốc 8: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
{
    // Giải thích dòng gốc 9: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Pixel nguồn và output cell: màu hiển thị cùng cờ solid 0/1.
    // Giải thích dòng gốc 10: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
    [ReadOnly] public NativeArray<Color32> TexturePixels;
    // Giải thích dòng gốc 11: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
    [WriteOnly] public NativeArray<Color32> CellColors;
    // Giải thích dòng gốc 12: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
    [WriteOnly] public NativeArray<byte> CellSolid;
    // Giải thích dòng gốc 13: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public int TextureWidth;
    // Giải thích dòng gốc 14: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public int TextureHeight;
    // Giải thích dòng gốc 15: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public int GridWidth;
    // Giải thích dòng gốc 16: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public int SampleStep;
    // Giải thích dòng gốc 17: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public byte AlphaLimit;

    // Giải thích dòng gốc 19: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Đổi một index cell sang pixel source, áp alpha threshold và ghi dữ liệu lưới.</summary>
    // Giải thích dòng gốc 20: Khai báo hàm Execute với tham số trong ngoặc để thực hiện một hành vi.
    public void Execute(int index)
    // Giải thích dòng gốc 21: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 22: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int cellX = index % GridWidth;
        // Giải thích dòng gốc 23: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int cellY = index / GridWidth;
        // Giải thích dòng gốc 24: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int sourceX = cellX * SampleStep;
        // Giải thích dòng gốc 25: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int sourceY = cellY * SampleStep;
        // Giải thích dòng gốc 26: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (sourceX >= TextureWidth) sourceX = TextureWidth - 1;
        // Giải thích dòng gốc 27: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (sourceY >= TextureHeight) sourceY = TextureHeight - 1;

        // Giải thích dòng gốc 29: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        Color32 color = TexturePixels[sourceY * TextureWidth + sourceX];
        // Giải thích dòng gốc 30: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        bool solid = color.a > AlphaLimit;
        // Giải thích dòng gốc 31: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (solid) color.a = 255;

        // Giải thích dòng gốc 33: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        CellColors[index] = color;
        // Giải thích dòng gốc 34: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        CellSolid[index] = solid ? (byte)1 : (byte)0;
    // Giải thích dòng gốc 35: Đóng khối code hiện tại.
    }
// Giải thích dòng gốc 36: Đóng khối code hiện tại.
}
