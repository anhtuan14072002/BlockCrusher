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
// Giải thích dòng gốc 7: Khai báo struct BuildChunkMeshJob là kiểu dữ liệu value type.
internal struct BuildChunkMeshJob : IJob
// Giải thích dòng gốc 8: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
{
    // Giải thích dòng gốc 9: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Cell input, visited mask và các native list output tạo thành mesh chunk.
    // Giải thích dòng gốc 10: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
    [ReadOnly] public NativeArray<Color32> CellColors;
    // Giải thích dòng gốc 11: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
    [ReadOnly] public NativeArray<byte> CellSolid;
    // Giải thích dòng gốc 12: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public NativeArray<byte> Visited;
    // Giải thích dòng gốc 13: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public NativeList<Vector3> Vertices;
    // Giải thích dòng gốc 14: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public NativeList<Color32> Colors;
    // Giải thích dòng gốc 15: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public NativeList<Vector2> Uvs;
    // Giải thích dòng gốc 16: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public NativeList<int> Indices;
    // Giải thích dòng gốc 17: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public int GridWidth;
    // Giải thích dòng gốc 18: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public int StartX;
    // Giải thích dòng gốc 19: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public int StartY;
    // Giải thích dòng gốc 20: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public int ChunkWidth;
    // Giải thích dòng gốc 21: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public int ChunkHeight;
    // Giải thích dòng gốc 22: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public float CellSize;
    // Giải thích dòng gốc 23: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public Vector3 Offset;
    // Giải thích dòng gốc 24: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public byte UseVoxelDetail;
    // Giải thích dòng gốc 25: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public byte ExtrudeMergedQuads;
    // Giải thích dòng gốc 26: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public byte MergeAnySolid;
    // Giải thích dòng gốc 27: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public float DetailVoxelScale;
    // Giải thích dòng gốc 28: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public float RandomYRotation;
    // Giải thích dòng gốc 29: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    public float DetailDepth;

    // Giải thích dòng gốc 31: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Chuyển toàn bộ input sang mesh builder; builder tạo quad/voxel không cấp phát GC.</summary>
    // Giải thích dòng gốc 32: Khai báo hàm Execute với tham số trong ngoặc để thực hiện một hành vi.
    public void Execute()
    // Giải thích dòng gốc 33: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 34: Tạo một instance mới của kiểu dữ liệu được chỉ định.
        new TextureBlockSpawner.TextureBlockMeshBuilder
        // Giải thích dòng gốc 35: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 36: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            CellColors = CellColors,
            // Giải thích dòng gốc 37: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            CellSolid = CellSolid,
            // Giải thích dòng gốc 38: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Visited = Visited,
            // Giải thích dòng gốc 39: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Vertices = Vertices,
            // Giải thích dòng gốc 40: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Colors = Colors,
            // Giải thích dòng gốc 41: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Uvs = Uvs,
            // Giải thích dòng gốc 42: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Indices = Indices,
            // Giải thích dòng gốc 43: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            GridWidth = GridWidth,
            // Giải thích dòng gốc 44: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            StartX = StartX,
            // Giải thích dòng gốc 45: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            StartY = StartY,
            // Giải thích dòng gốc 46: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            ChunkWidth = ChunkWidth,
            // Giải thích dòng gốc 47: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            ChunkHeight = ChunkHeight,
            // Giải thích dòng gốc 48: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            CellSize = CellSize,
            // Giải thích dòng gốc 49: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Offset = Offset,
            // Giải thích dòng gốc 50: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            UseVoxelDetail = UseVoxelDetail,
            // Giải thích dòng gốc 51: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            ExtrudeMergedQuads = ExtrudeMergedQuads,
            // Giải thích dòng gốc 52: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            MergeAnySolid = MergeAnySolid,
            // Giải thích dòng gốc 53: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            DetailVoxelScale = DetailVoxelScale,
            // Giải thích dòng gốc 54: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            RandomYRotation = RandomYRotation,
            // Giải thích dòng gốc 55: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            DetailDepth = DetailDepth
        // Giải thích dòng gốc 56: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
        }.Execute();
    // Giải thích dòng gốc 57: Đóng khối code hiện tại.
    }
// Giải thích dòng gốc 58: Đóng khối code hiện tại.
}
