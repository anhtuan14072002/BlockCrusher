// Giải thích dòng gốc 1: Nạp namespace Unity.Entities để file dùng được các kiểu và API trong đó.
using Unity.Entities;

// Giải thích dòng gốc 3: Khai báo struct ReleasedBlockAuthoringComponent là kiểu dữ liệu value type.
public struct ReleasedBlockAuthoringComponent : IComponentData
// Giải thích dòng gốc 4: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
{
    // Giải thích dòng gốc 5: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Đánh dấu entity authoring đã được baker tạo cho prefab debris.
    // Giải thích dòng gốc 6: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public Entity ReleasedBlockPrefab;
    // Giải thích dòng gốc 7: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public float Scale;
// Giải thích dòng gốc 8: Đóng khối code hiện tại.
}
