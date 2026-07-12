// Giải thích dòng gốc 1: Nạp namespace Unity.Burst để file dùng được các kiểu và API trong đó.
using Unity.Burst;
// Giải thích dòng gốc 2: Nạp namespace Unity.Collections để file dùng được các kiểu và API trong đó.
using Unity.Collections;
// Giải thích dòng gốc 3: Nạp namespace Unity.Jobs để file dùng được các kiểu và API trong đó.
using Unity.Jobs;
// Giải thích dòng gốc 4: Nạp namespace Unity.Mathematics để file dùng được các kiểu và API trong đó.
using Unity.Mathematics;
// Giải thích dòng gốc 5: Nạp namespace Unity.Transforms để file dùng được các kiểu và API trong đó.
using Unity.Transforms;

// Giải thích dòng gốc 7: Gắn attribute [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)] cho khai báo nằm ngay bên dưới.
[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
// Giải thích dòng gốc 8: Khai báo struct PrepareRenderFrameJob là kiểu dữ liệu value type.
internal struct PrepareRenderFrameJob : IJob
// Giải thích dòng gốc 9: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
{
    // Giải thích dòng gốc 10: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Component ECS đầu vào và buffer GPU-ready được ghi cho một render frame.
    // Giải thích dòng gốc 11: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
    [ReadOnly] public NativeArray<LocalTransform> Transforms;
    // Giải thích dòng gốc 12: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
    [ReadOnly] public NativeArray<ReleasedBlockComponent> Blocks;
    // Giải thích dòng gốc 13: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
    [WriteOnly] public NativeArray<float4x4> Matrices;
    // Giải thích dòng gốc 14: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
    [WriteOnly] public NativeArray<float4> Colors;
    // Giải thích dòng gốc 15: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public NativeReference<int> Count;
    // Giải thích dòng gốc 16: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public int OwnerId;

    // Giải thích dòng gốc 18: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Lọc debris theo OwnerId rồi xuất matrix transform và màu cho DrawMeshInstanced.</summary>
    // Giải thích dòng gốc 19: Khai báo hàm Execute với tham số trong ngoặc để thực hiện một hành vi.
    public void Execute()
    // Giải thích dòng gốc 20: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 21: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int count = 0;
        // Giải thích dòng gốc 22: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int length = math.min(Transforms.Length, Blocks.Length);
        // Giải thích dòng gốc 23: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int i = 0; i < length && count < Matrices.Length; i++)
        // Giải thích dòng gốc 24: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 25: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            ReleasedBlockComponent block = Blocks[i];
            // Giải thích dòng gốc 26: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (block.OwnerId != OwnerId) continue;

            // Giải thích dòng gốc 28: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            LocalTransform transformData = Transforms[i];
            // Giải thích dòng gốc 29: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Matrices[count] = float4x4.TRS(transformData.Position, transformData.Rotation, new float3(transformData.Scale));
            // Giải thích dòng gốc 30: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Colors[count] = block.Color;
            // Giải thích dòng gốc 31: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
            count++;
        // Giải thích dòng gốc 32: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 34: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        Count.Value = count;
    // Giải thích dòng gốc 35: Đóng khối code hiện tại.
    }
// Giải thích dòng gốc 36: Đóng khối code hiện tại.
}
