// Giải thích dòng gốc 1: Nạp namespace Unity.Collections để file dùng được các kiểu và API trong đó.
using Unity.Collections;
// Giải thích dòng gốc 2: Nạp namespace Unity.Jobs để file dùng được các kiểu và API trong đó.
using Unity.Jobs;
// Giải thích dòng gốc 3: Nạp namespace Unity.Mathematics để file dùng được các kiểu và API trong đó.
using Unity.Mathematics;

// Giải thích dòng gốc 5: Khai báo class RenderFrameData để chứa dữ liệu và hành vi liên quan.
internal sealed class RenderFrameData
// Giải thích dòng gốc 6: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
{
    // Giải thích dòng gốc 7: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Native output của render job: transform, màu, số instance, dependency và trạng thái frame.
    // Giải thích dòng gốc 8: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public NativeArray<float4x4> Matrices;
    // Giải thích dòng gốc 9: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public NativeArray<float4> Colors;
    // Giải thích dòng gốc 10: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public NativeReference<int> Count;
    // Giải thích dòng gốc 11: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public JobHandle Handle;
    // Giải thích dòng gốc 12: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public bool Pending;

    // Giải thích dòng gốc 14: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Cấp phát buffer persistent đủ chứa tối đa debris của spawner.</summary>
    // Giải thích dòng gốc 15: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
    public RenderFrameData(int capacity)
    // Giải thích dòng gốc 16: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 17: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        Matrices = new NativeArray<float4x4>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        // Giải thích dòng gốc 18: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        Colors = new NativeArray<float4>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        // Giải thích dòng gốc 19: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        Count = new NativeReference<int>(Allocator.Persistent);
    // Giải thích dòng gốc 20: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 22: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Hoàn trả toàn bộ native allocation của frame.</summary>
    // Giải thích dòng gốc 23: Khai báo hàm Dispose với tham số trong ngoặc để thực hiện một hành vi.
    public void Dispose()
    // Giải thích dòng gốc 24: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 25: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (Matrices.IsCreated) Matrices.Dispose();
        // Giải thích dòng gốc 26: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (Colors.IsCreated) Colors.Dispose();
        // Giải thích dòng gốc 27: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (Count.IsCreated) Count.Dispose();
    // Giải thích dòng gốc 28: Đóng khối code hiện tại.
    }
// Giải thích dòng gốc 29: Đóng khối code hiện tại.
}
