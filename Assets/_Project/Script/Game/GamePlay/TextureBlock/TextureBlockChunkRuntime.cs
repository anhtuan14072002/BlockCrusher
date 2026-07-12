// Giải thích dòng gốc 1: Nạp namespace Unity.Collections để file dùng được các kiểu và API trong đó.
using Unity.Collections;
// Giải thích dòng gốc 2: Nạp namespace Unity.Entities để file dùng được các kiểu và API trong đó.
using Unity.Entities;
// Giải thích dòng gốc 3: Nạp namespace Unity.Physics để file dùng được các kiểu và API trong đó.
using Unity.Physics;
// Giải thích dòng gốc 4: Nạp namespace UnityEngine để file dùng được các kiểu và API trong đó.
using UnityEngine;
// Giải thích dòng gốc 5: Nạp namespace PhysicsCollider = Unity.Physics.Collider để file dùng được các kiểu và API trong đó.
using PhysicsCollider = Unity.Physics.Collider;

// Giải thích dòng gốc 7: Khai báo struct ChunkRuntime là kiểu dữ liệu value type.
internal struct ChunkRuntime
// Giải thích dòng gốc 8: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
{
    // Giải thích dòng gốc 9: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Unity Mesh/Renderer hiển thị phần cell còn nguyên của chunk.
    // Giải thích dòng gốc 10: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public Mesh Mesh;
    // Giải thích dòng gốc 11: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public MeshRenderer Renderer;
    // Giải thích dòng gốc 12: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Entity và blob collider ECS đại diện phần solid của chunk.
    // Giải thích dòng gốc 13: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public Entity PhysicsEntity;
    // Giải thích dòng gốc 14: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public BlobAssetReference<PhysicsCollider> PhysicsCollider;
    // Giải thích dòng gốc 15: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Buffer tạm khi greedy-mesh: đánh dấu cell đã được gộp thành quad/voxel.
    // Giải thích dòng gốc 16: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public NativeArray<byte> Visited;
    // Giải thích dòng gốc 17: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public NativeList<Vector3> Vertices;
    // Giải thích dòng gốc 18: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public NativeList<Color32> Colors;
    // Giải thích dòng gốc 19: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public NativeList<Vector2> Uvs;
    // Giải thích dòng gốc 20: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public NativeList<int> Indices;
    // Giải thích dòng gốc 21: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Bộ buffer riêng cho mesh collider để không ảnh hưởng mesh render.
    // Giải thích dòng gốc 22: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public NativeArray<byte> ColliderVisited;
    // Giải thích dòng gốc 23: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public NativeList<Vector3> ColliderVertices;
    // Giải thích dòng gốc 24: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public NativeList<Color32> ColliderColors;
    // Giải thích dòng gốc 25: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public NativeList<Vector2> ColliderUvs;
    // Giải thích dòng gốc 26: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public NativeList<int> ColliderIndices;
    // Giải thích dòng gốc 27: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public int StartX;
    // Giải thích dòng gốc 28: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public int StartY;
    // Giải thích dòng gốc 29: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public int Width;
    // Giải thích dòng gốc 30: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public int Height;
    // Giải thích dòng gốc 31: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public bool IsDetailed;
// Giải thích dòng gốc 32: Đóng khối code hiện tại.
}
