// Giải thích dòng gốc 1: Nạp namespace Unity.Entities để file dùng được các kiểu và API trong đó.
using Unity.Entities;
// Giải thích dòng gốc 2: Nạp namespace Unity.Mathematics để file dùng được các kiểu và API trong đó.
using Unity.Mathematics;

// Giải thích dòng gốc 4: Khai báo struct ReleasedBlockComponent là kiểu dữ liệu value type.
public struct ReleasedBlockComponent : IComponentData
// Giải thích dòng gốc 5: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
{
    // Giải thích dòng gốc 6: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Ngưỡng vận tốc và số tick cần đứng yên để debris được coi là đã ổn định.
    // Giải thích dòng gốc 7: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    public const float SettleSpeed = 0.05f;
    // Giải thích dòng gốc 8: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    public const byte SettleFrames = 6;

    // Giải thích dòng gốc 10: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Owner dùng để mỗi TextureBlockSpawner chỉ điều khiển/render debris của chính nó.
    // Giải thích dòng gốc 11: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public int OwnerId;
    // Giải thích dòng gốc 12: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public float4 Color;
    // Giải thích dòng gốc 13: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public float LockedZ;
    // Giải thích dòng gốc 14: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public float MaxPlanarSpeed;
    // Giải thích dòng gốc 15: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public byte StableFrames;
// Giải thích dòng gốc 16: Đóng khối code hiện tại.
}
