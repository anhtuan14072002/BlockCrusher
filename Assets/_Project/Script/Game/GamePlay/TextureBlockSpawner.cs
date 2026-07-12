// Giải thích dòng gốc 1: Nạp namespace System.Collections.Generic để file dùng được các kiểu và API trong đó.
using System.Collections.Generic;
// Giải thích dòng gốc 2: Nạp namespace Unity.Burst để file dùng được các kiểu và API trong đó.
using Unity.Burst;
// Giải thích dòng gốc 3: Nạp namespace Unity.Collections để file dùng được các kiểu và API trong đó.
using Unity.Collections;
// Giải thích dòng gốc 4: Nạp namespace Unity.Entities để file dùng được các kiểu và API trong đó.
using Unity.Entities;
// Giải thích dòng gốc 5: Nạp namespace Unity.Jobs để file dùng được các kiểu và API trong đó.
using Unity.Jobs;
// Giải thích dòng gốc 6: Nạp namespace Unity.Mathematics để file dùng được các kiểu và API trong đó.
using Unity.Mathematics;
// Giải thích dòng gốc 7: Nạp namespace Unity.Physics để file dùng được các kiểu và API trong đó.
using Unity.Physics;
// Giải thích dòng gốc 8: Nạp namespace Unity.Transforms để file dùng được các kiểu và API trong đó.
using Unity.Transforms;
// Giải thích dòng gốc 9: Nạp namespace UnityEngine để file dùng được các kiểu và API trong đó.
using UnityEngine;
// Giải thích dòng gốc 10: Nạp namespace UnityEngine.Rendering để file dùng được các kiểu và API trong đó.
using UnityEngine.Rendering;
// Giải thích dòng gốc 11: Nạp namespace Collider = Unity.Physics.Collider để file dùng được các kiểu và API trong đó.
using Collider = Unity.Physics.Collider;
// Giải thích dòng gốc 12: Nạp namespace PhysicsMaterial = Unity.Physics.Material để file dùng được các kiểu và API trong đó.
using PhysicsMaterial = Unity.Physics.Material;
// Giải thích dòng gốc 13: Nạp namespace RenderMaterial = UnityEngine.Material để file dùng được các kiểu và API trong đó.
using RenderMaterial = UnityEngine.Material;

// Giải thích dòng gốc 15: Khai báo class TextureBlockSpawner để chứa dữ liệu và hành vi liên quan.
public sealed class TextureBlockSpawner : MonoBehaviour
// Giải thích dòng gốc 16: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
{
    // Giải thích dòng gốc 17: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Danh sách tất cả spawner đang bật để tool gameplay có thể tác động theo world-space.
    // Giải thích dòng gốc 18: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    private static readonly List<TextureBlockSpawner> ActiveSpawners = new();
    // Giải thích dòng gốc 19: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // ID duy nhất dùng để gắn debris ECS về đúng spawner sở hữu nó.
    // Giải thích dòng gốc 20: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    private static int _nextOwnerId = 1;

    // Giải thích dòng gốc 22: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Texture nguồn được chuyển thành lưới cell/voxel.
    // Giải thích dòng gốc 23: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    [SerializeField] private Texture2D _texture;
    // Giải thích dòng gốc 24: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Parent chứa các GameObject chunk được tạo ở runtime.
    // Giải thích dòng gốc 25: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    [SerializeField] private Transform _container;
    // Giải thích dòng gốc 26: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Material dùng để render mesh chunk còn nguyên.
    // Giải thích dòng gốc 27: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    [SerializeField] private RenderMaterial _chunkMaterial;
    // Giải thích dòng gốc 28: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Kích thước world-space của một pixel/cell.
    // Giải thích dòng gốc 29: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField] private float _pixelSize = 0.12f;
    // Giải thích dòng gốc 30: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Bước lấy mẫu texture; tăng giá trị sẽ giảm mật độ lưới.
    // Giải thích dòng gốc 31: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField, Range(1, 16)] private int _sampleStep = 1;
    // Giải thích dòng gốc 32: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Alpha tối thiểu để một pixel được coi là block đặc.
    // Giải thích dòng gốc 33: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField, Range(0f, 1f)] private float _alphaThreshold = 0.1f;
    // Giải thích dòng gốc 34: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Độ dày collider của chunk trong trục Z.
    // Giải thích dòng gốc 35: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField] private float _chunkColliderDepth = 0.25f;
    // Giải thích dòng gốc 36: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Bán kính giải phóng cell tại vị trí tiếp xúc của lưỡi cưa.
    // Giải thích dòng gốc 37: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField] private float _sawReleaseRadius = 0.18f;
    // Giải thích dòng gốc 38: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Bật mesh voxel 3D ngay từ lúc chunk được sinh ra.
    // Giải thích dòng gốc 39: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField] private bool _renderVoxelDetailFromStart = true;
    // Giải thích dòng gốc 40: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Tỉ lệ voxel chi tiết so với kích thước cell, giúp chừa khe nhìn thấy được.
    // Giải thích dòng gốc 41: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField, Range(0.75f, 1f)] private float _detailVoxelScale = 0.94f;
    // Giải thích dòng gốc 42: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Góc xoay Y ngẫu nhiên tối đa cho từng voxel chi tiết.
    // Giải thích dòng gốc 43: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField, Range(0f, 30f)] private float _voxelRandomYRotation = 20f;
    // Giải thích dòng gốc 44: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Giới hạn debris ECS được spawn trong một frame; 0 nghĩa là không giới hạn.
    // Giải thích dòng gốc 45: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    [SerializeField, Min(0)] private int _maxPhysicsDebrisPerFrame;
    // Giải thích dòng gốc 46: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Số cell mỗi cạnh của một chunk mesh.
    // Giải thích dòng gốc 47: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField, Range(8, 64)] private int _chunkSize = 24;
    // Giải thích dòng gốc 48: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Số chunk tối đa được rebuild mesh/collider trong một frame.
    // Giải thích dòng gốc 49: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField, Range(1, 8)] private int _maxChunkRebuildsPerFrame = 2;
    // Giải thích dòng gốc 50: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Damping tuyến tính của block đã tách ra.
    // Giải thích dòng gốc 51: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField, Range(0f, 20f)] private float _releasedBlockDamping = 2.5f;
    // Giải thích dòng gốc 52: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Damping góc của block đã tách ra.
    // Giải thích dòng gốc 53: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField, Range(0f, 20f)] private float _releasedBlockAngularDamping = 4f;
    // Giải thích dòng gốc 54: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Khối lượng áp dụng cho debris ECS.
    // Giải thích dòng gốc 55: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField, Range(0.01f, 10f)] private float _releasedBlockMass = 0.1f;
    // Giải thích dòng gốc 56: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Dung lượng tối đa của debris đang tồn tại và buffer render.
    // Giải thích dòng gốc 57: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField, Range(128, 10000)] private int _maxReleasedPhysicsBlocks = 5000;
    // Giải thích dòng gốc 58: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Khoảng frame giữa các lần chuẩn bị dữ liệu render debris.
    // Giải thích dòng gốc 59: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField, Range(1, 4)] private int _releasedBlockRenderInterval = 2;
    // Giải thích dòng gốc 60: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Ma sát và độ nảy của collider debris/wall ECS.
    // Giải thích dòng gốc 61: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField, Range(0f, 1f)] private float _physicsFriction = 0.12f;
    // Giải thích dòng gốc 62: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    [SerializeField, Range(0f, 1f)] private float _physicsRestitution;
    // Giải thích dòng gốc 63: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Authoring mô tả mesh/material/collider cho block ECS được giải phóng.
    // Giải thích dòng gốc 64: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    [SerializeField] private ReleasedBlockAuthoring _releasedBlockAuthoring;
    // Giải thích dòng gốc 65: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Các Transform tường giới hạn vùng debris ECS có thể di chuyển.
    // Giải thích dòng gốc 66: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    [SerializeField] private Transform[] _releasedBlockWalls;
    // Giải thích dòng gốc 67: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Đặt tâm texture vào gốc local thay vì bắt đầu tại (0, 0).
    // Giải thích dòng gốc 68: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField] private bool _centerTexture = true;
    // Giải thích dòng gốc 69: Comment ghi chú cho người đọc; compiler bỏ qua dòng này.
    // Tự tạo lưới khi component khởi tạo.
    // Giải thích dòng gốc 70: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField] private bool _spawnOnAwake = true;

    // Giải thích dòng gốc 72: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    private readonly List<ChunkRuntime> _chunks = new();
    // Giải thích dòng gốc 73: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    private readonly List<int> _dirtyChunks = new(16);
    // Giải thích dòng gốc 74: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    private readonly List<int> _scheduledChunkRebuilds = new(8);
    // Giải thích dòng gốc 75: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    private readonly List<JobHandle> _scheduledChunkHandles = new(8);
    // Giải thích dòng gốc 76: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    private readonly List<Entity> _releasedBlockEntities = new(1024);
    // Giải thích dòng gốc 77: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    private readonly List<Entity> _releasedBlockWallEntities = new(4);
    // Giải thích dòng gốc 78: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    private readonly List<BlobAssetReference<Collider>> _releasedBlockWallColliders = new(4);
    // Giải thích dòng gốc 79: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    private readonly RenderFrameData[] _renderFrames = new RenderFrameData[2];
    // Giải thích dòng gốc 80: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private Matrix4x4[][] _renderBatchMatrices;
    // Giải thích dòng gốc 81: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private Vector4[][] _renderBatchColors;
    // Giải thích dòng gốc 82: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private int[] _renderBatchCounts;
    // Giải thích dòng gốc 83: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private int _renderBatchCount;
    // Giải thích dòng gốc 84: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private NativeArray<Color32> _cellColors;
    // Giải thích dòng gốc 85: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private NativeArray<byte> _cellSolid;
    // Giải thích dòng gốc 86: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private World _ecsWorld;
    // Giải thích dòng gốc 87: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private EntityManager _entityManager;
    // Giải thích dòng gốc 88: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private EntityQuery _releasedBlockQuery;
    // Giải thích dòng gốc 89: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private EntityArchetype _releasedBlockArchetype;
    // Giải thích dòng gốc 90: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private BlobAssetReference<Collider> _releasedBlockCollider;
    // Giải thích dòng gốc 91: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private Mesh _releasedBlockMesh;
    // Giải thích dòng gốc 92: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private RenderMaterial _releasedBlockMaterial;
    // Giải thích dòng gốc 93: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private MaterialPropertyBlock _releasedBlockPropertyBlock;
    // Giải thích dòng gốc 94: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private byte[] _chunkDirty;
    // Giải thích dòng gốc 95: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private Transform _runtimeParent;
    // Giải thích dòng gốc 96: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private RenderMaterial _runtimeChunkMaterial;
    // Giải thích dòng gốc 97: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private Vector3 _offset;
    // Giải thích dòng gốc 98: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private int _ownerId;
    // Giải thích dòng gốc 99: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private int _gridWidth;
    // Giải thích dòng gốc 100: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private int _gridHeight;
    // Giải thích dòng gốc 101: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private int _chunkColumns;
    // Giải thích dòng gốc 102: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private float _cellSize;
    // Giải thích dòng gốc 103: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    private float _releasedBlockScale = 1f;
    // Giải thích dòng gốc 104: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    private int _physicsDebrisFrame = -1;
    // Giải thích dòng gốc 105: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private int _physicsDebrisSpawnedThisFrame;
    // Giải thích dòng gốc 106: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private bool _hasReleasedBlockQuery;
    // Giải thích dòng gốc 107: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private bool _hasPendingSawPush;
    // Giải thích dòng gốc 108: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private Vector3 _pendingSawCenter;
    // Giải thích dòng gốc 109: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private Vector3 _pendingSawDirection;
    // Giải thích dòng gốc 110: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private float _pendingSawSpeed;
    // Giải thích dòng gốc 111: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private float _pendingSawOutwardForce;
    // Giải thích dòng gốc 112: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private float _pendingSawTangentialForce;
    // Giải thích dòng gốc 113: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private float _pendingSawSpinDirection;
    // Giải thích dòng gốc 114: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private float _pendingSawRadius;
    // Giải thích dòng gốc 115: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private float _pendingSawMaxVelocity;
    // Giải thích dòng gốc 116: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    private int _displayRenderFrame = -1;
    // Giải thích dòng gốc 117: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    // Giải thích dòng gốc 119: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Yêu cầu mọi spawner đang hoạt động giải phóng cell tại điểm world-space của cưa.</summary>
    // Giải thích dòng gốc 120: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
    public static bool ReleaseAtWorldForActiveSpawners(Vector3 worldPoint, Vector3 pressDirection, float pressSpeed,
        // Giải thích dòng gốc 121: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
        float outwardForce, float tangentialForce, float spinDirection, float bladeRadius, float sideDamping,
        // Giải thích dòng gốc 122: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
        float maxVelocity)
    // Giải thích dòng gốc 123: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 124: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        bool releasedAny = false;

        // Giải thích dòng gốc 126: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int i = ActiveSpawners.Count - 1; i >= 0; i--)
        // Giải thích dòng gốc 127: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 128: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            TextureBlockSpawner spawner = ActiveSpawners[i];
            // Giải thích dòng gốc 129: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (spawner == null)
            // Giải thích dòng gốc 130: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
            {
                // Giải thích dòng gốc 131: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                ActiveSpawners.RemoveAt(i);
                // Giải thích dòng gốc 132: Bỏ qua phần còn lại của lần lặp hiện tại và sang lần tiếp theo.
                continue;
            // Giải thích dòng gốc 133: Đóng khối code hiện tại.
            }

            // Giải thích dòng gốc 135: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            releasedAny |= spawner.ReleaseAtWorld(worldPoint, pressDirection, pressSpeed, outwardForce, tangentialForce,
                // Giải thích dòng gốc 136: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                spinDirection, bladeRadius, sideDamping, maxVelocity);
        // Giải thích dòng gốc 137: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 139: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
        return releasedAny;
    // Giải thích dòng gốc 140: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 142: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Kiểm tra có cell đặc nào trong bán kính world-space trên bất kỳ spawner nào không.</summary>
    // Giải thích dòng gốc 143: Khai báo hàm HasSolidAtWorldForActiveSpawners với tham số trong ngoặc để thực hiện một hành vi.
    public static bool HasSolidAtWorldForActiveSpawners(Vector3 worldPoint, float radius)
    // Giải thích dòng gốc 144: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 145: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int i = ActiveSpawners.Count - 1; i >= 0; i--)
        // Giải thích dòng gốc 146: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 147: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            TextureBlockSpawner spawner = ActiveSpawners[i];
            // Giải thích dòng gốc 148: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (spawner == null)
            // Giải thích dòng gốc 149: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
            {
                // Giải thích dòng gốc 150: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                ActiveSpawners.RemoveAt(i);
                // Giải thích dòng gốc 151: Bỏ qua phần còn lại của lần lặp hiện tại và sang lần tiếp theo.
                continue;
            // Giải thích dòng gốc 152: Đóng khối code hiện tại.
            }

            // Giải thích dòng gốc 154: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (spawner.HasSolidAtWorld(worldPoint, radius))
                // Giải thích dòng gốc 155: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return true;
        // Giải thích dòng gốc 156: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 158: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
        return false;
    // Giải thích dòng gốc 159: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 161: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Đẩy debris nằm trong vùng conveyor theo hướng và vận tốc đã cho.</summary>
    // Giải thích dòng gốc 162: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
    public static void ApplyConveyorForActiveSpawners(Bounds bounds, Vector3 direction, float speed, float acceleration,
        // Giải thích dòng gốc 163: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
        float deltaTime)
    // Giải thích dòng gốc 164: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 165: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int i = 0; i < ActiveSpawners.Count; i++)
            // Giải thích dòng gốc 166: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
            ActiveSpawners[i].ApplyConveyor(bounds, direction, speed, acceleration, deltaTime);
    // Giải thích dòng gốc 167: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 169: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Xóa debris thuộc phạm vi world-space, thường được gọi bởi vùng đích/thu gom.</summary>
    // Giải thích dòng gốc 170: Khai báo hàm ClearReleasedBlocksForActiveSpawners với tham số trong ngoặc để thực hiện một hành vi.
    public static void ClearReleasedBlocksForActiveSpawners(Bounds bounds)
    // Giải thích dòng gốc 171: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 172: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int i = 0; i < ActiveSpawners.Count; i++)
            // Giải thích dòng gốc 173: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
            ActiveSpawners[i].DespawnInBounds(bounds);
    // Giải thích dòng gốc 174: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 176: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Hút debris trong vùng hộp về đầu hút và xóa block khi tới gần tâm hút.</summary>
    // Giải thích dòng gốc 177: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
    public static void ApplySuctionForActiveSpawners(Vector3 origin, Quaternion rotation, Vector3 boxSize,
        // Giải thích dòng gốc 178: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
        float force, float acceleration, float maxVelocity, float arrivalDamping, float destroyRadius, float deltaTime)
    // Giải thích dòng gốc 179: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 180: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int i = 0; i < ActiveSpawners.Count; i++)
            // Giải thích dòng gốc 181: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
            ActiveSpawners[i].ApplySuction(origin, rotation, boxSize, force, acceleration, maxVelocity, arrivalDamping,
                // Giải thích dòng gốc 182: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                destroyRadius, deltaTime);
    // Giải thích dòng gốc 183: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 185: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Đăng ký instance vào danh sách spawner global và cấp owner ID nếu cần.</summary>
    // Giải thích dòng gốc 186: Khai báo hàm OnEnable với tham số trong ngoặc để thực hiện một hành vi.
    private void OnEnable()
    // Giải thích dòng gốc 187: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 188: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_ownerId == 0)
            // Giải thích dòng gốc 189: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _ownerId = _nextOwnerId++;

        // Giải thích dòng gốc 191: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (!ActiveSpawners.Contains(this))
            // Giải thích dòng gốc 192: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            ActiveSpawners.Add(this);
    // Giải thích dòng gốc 193: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 195: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Ngừng nhận lệnh gameplay global khi component bị tắt.</summary>
    // Giải thích dòng gốc 196: Khai báo hàm OnDisable với tham số trong ngoặc để thực hiện một hành vi.
    private void OnDisable()
    // Giải thích dòng gốc 197: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 198: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        ActiveSpawners.Remove(this);
    // Giải thích dòng gốc 199: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 201: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Tạo lưới/chunk ngay khi scene load nếu cấu hình cho phép.</summary>
    // Giải thích dòng gốc 202: Khai báo hàm Awake với tham số trong ngoặc để thực hiện một hành vi.
    private void Awake()
    // Giải thích dòng gốc 203: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 204: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_spawnOnAwake)
            // Giải thích dòng gốc 205: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            Spawn();
    // Giải thích dòng gốc 206: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 208: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Hoàn trả native collection, mesh, collider và entity ECS đã sở hữu.</summary>
    // Giải thích dòng gốc 209: Khai báo hàm OnDestroy với tham số trong ngoặc để thực hiện một hành vi.
    private void OnDestroy()
    // Giải thích dòng gốc 210: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 211: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        DisposeChunks();
        // Giải thích dòng gốc 212: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        DisposeReleasedBlocks();
        // Giải thích dòng gốc 213: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        DisposeReleasedBlockWalls();
        // Giải thích dòng gốc 214: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        DisposeReleasedBlockResources();
        // Giải thích dòng gốc 215: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        DisposeCells();
        // Giải thích dòng gốc 216: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        DisposeEcsQuery();
    // Giải thích dòng gốc 217: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 219: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Áp dụng saw force và đồng bộ render debris sau khi gameplay đã cập nhật frame.</summary>
    // Giải thích dòng gốc 220: Khai báo hàm LateUpdate với tham số trong ngoặc để thực hiện một hành vi.
    private void LateUpdate()
    // Giải thích dòng gốc 221: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 222: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        ApplyPendingSawPush();
        // Giải thích dòng gốc 223: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        DrawReleasedBlocks();

        // Giải thích dòng gốc 225: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_dirtyChunks.Count == 0)
            // Giải thích dòng gốc 226: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return;

        // Giải thích dòng gốc 228: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int rebuildCount = Mathf.Min(_maxChunkRebuildsPerFrame, _dirtyChunks.Count);
        // Giải thích dòng gốc 229: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        _scheduledChunkRebuilds.Clear();
        // Giải thích dòng gốc 230: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int i = 0; i < rebuildCount; i++)
        // Giải thích dòng gốc 231: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 232: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            int lastIndex = _dirtyChunks.Count - 1;
            // Giải thích dòng gốc 233: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            int chunkIndex = _dirtyChunks[lastIndex];
            // Giải thích dòng gốc 234: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _dirtyChunks.RemoveAt(lastIndex);

            // Giải thích dòng gốc 236: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (_chunkDirty != null && chunkIndex >= 0 && chunkIndex < _chunkDirty.Length)
                // Giải thích dòng gốc 237: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                _chunkDirty[chunkIndex] = 0;

            // Giải thích dòng gốc 239: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _scheduledChunkRebuilds.Add(chunkIndex);
        // Giải thích dòng gốc 240: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 242: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        ScheduleAndApplyChunkRebuilds();
    // Giải thích dòng gốc 243: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 245: Gắn attribute [ContextMenu("Spawn")] cho khai báo nằm ngay bên dưới.
    [ContextMenu("Spawn")]
    // Giải thích dòng gốc 246: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Đọc texture, tạo cell grid, ECS owner và toàn bộ chunk render/collider ban đầu.</summary>
    // Giải thích dòng gốc 247: Khai báo hàm Spawn với tham số trong ngoặc để thực hiện một hành vi.
    public void Spawn()
    // Giải thích dòng gốc 248: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 249: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_texture == null)
            // Giải thích dòng gốc 250: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return;

        // Giải thích dòng gốc 252: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        Clear();

        // Giải thích dòng gốc 254: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
        Color32[] pixels;
        // Giải thích dòng gốc 255: Bắt đầu khối try để chạy code có thể phát sinh exception.
        try
        // Giải thích dòng gốc 256: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 257: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            pixels = _texture.GetPixels32();
        // Giải thích dòng gốc 258: Đóng khối code hiện tại.
        }
        // Giải thích dòng gốc 259: Bắt exception phát sinh từ khối try và xử lý lỗi.
        catch (UnityException)
        // Giải thích dòng gốc 260: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 261: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            Debug.LogError("TextureBlockSpawner needs a readable texture.", this);
            // Giải thích dòng gốc 262: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return;
        // Giải thích dòng gốc 263: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 265: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int width = _texture.width;
        // Giải thích dòng gốc 266: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int height = _texture.height;
        // Giải thích dòng gốc 267: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _runtimeParent = _container != null ? _container : transform;
        // Giải thích dòng gốc 268: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _cellSize = _pixelSize * _sampleStep;
        // Giải thích dòng gốc 269: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _gridWidth = Mathf.CeilToInt(width / (float)_sampleStep);
        // Giải thích dòng gốc 270: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _gridHeight = Mathf.CeilToInt(height / (float)_sampleStep);
        // Giải thích dòng gốc 271: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _offset = _centerTexture
            // Giải thích dòng gốc 272: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
            ? new Vector3((_gridWidth - 1) * _cellSize * -0.5f, (_gridHeight - 1) * _cellSize * -0.5f, 0f)
            // Giải thích dòng gốc 273: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
            : Vector3.zero;

        // Giải thích dòng gốc 275: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        DisposeCells();
        // Giải thích dòng gốc 276: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _cellColors = new NativeArray<Color32>(_gridWidth * _gridHeight, Allocator.Persistent);
        // Giải thích dòng gốc 277: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _cellSolid = new NativeArray<byte>(_gridWidth * _gridHeight, Allocator.Persistent);

        // Giải thích dòng gốc 279: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        NativeArray<Color32> texturePixels = new NativeArray<Color32>(pixels, Allocator.TempJob);
        // Giải thích dòng gốc 280: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        TextureToCellsJob textureJob = new TextureToCellsJob
        // Giải thích dòng gốc 281: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 282: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            TexturePixels = texturePixels,
            // Giải thích dòng gốc 283: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            CellColors = _cellColors,
            // Giải thích dòng gốc 284: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            CellSolid = _cellSolid,
            // Giải thích dòng gốc 285: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            TextureWidth = width,
            // Giải thích dòng gốc 286: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            TextureHeight = height,
            // Giải thích dòng gốc 287: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            GridWidth = _gridWidth,
            // Giải thích dòng gốc 288: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            SampleStep = _sampleStep,
            // Giải thích dòng gốc 289: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            AlphaLimit = (byte)Mathf.RoundToInt(_alphaThreshold * 255f)
        // Giải thích dòng gốc 290: Đóng khối khởi tạo dữ liệu và kết thúc câu lệnh.
        };

        // Giải thích dòng gốc 292: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        textureJob.Schedule(_cellColors.Length, 64).Complete();
        // Giải thích dòng gốc 293: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        texturePixels.Dispose();

        // Giải thích dòng gốc 295: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _runtimeChunkMaterial = ResolveChunkMaterial();
        // Giải thích dòng gốc 296: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        CreateChunks();
    // Giải thích dòng gốc 297: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 299: Gắn attribute [ContextMenu("Clear")] cho khai báo nằm ngay bên dưới.
    [ContextMenu("Clear")]
    // Giải thích dòng gốc 300: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Xóa trạng thái spawn hiện có để có thể tạo lại texture grid sạch.</summary>
    // Giải thích dòng gốc 301: Khai báo hàm Clear với tham số trong ngoặc để thực hiện một hành vi.
    public void Clear()
    // Giải thích dòng gốc 302: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 303: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        DisposeCells();
        // Giải thích dòng gốc 304: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        DisposeChunks();
        // Giải thích dòng gốc 305: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        _chunks.Clear();
        // Giải thích dòng gốc 306: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        _dirtyChunks.Clear();
        // Giải thích dòng gốc 307: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        _scheduledChunkRebuilds.Clear();
        // Giải thích dòng gốc 308: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        _scheduledChunkHandles.Clear();
        // Giải thích dòng gốc 309: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _chunkDirty = null;
        // Giải thích dòng gốc 310: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        ClearReleasedBlockEntities();

        // Giải thích dòng gốc 312: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        Transform parent = _container != null ? _container : transform;
        // Giải thích dòng gốc 313: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int i = parent.childCount - 1; i >= 0; i--)
        // Giải thích dòng gốc 314: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 315: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Transform child = parent.GetChild(i);
            // Giải thích dòng gốc 316: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (Application.isPlaying)
                // Giải thích dòng gốc 317: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                Destroy(child.gameObject);
            // Giải thích dòng gốc 318: Chạy nhánh thay thế khi điều kiện if trước đó không đúng.
            else
                // Giải thích dòng gốc 319: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                DestroyImmediate(child.gameObject);
        // Giải thích dòng gốc 320: Đóng khối code hiện tại.
        }
    // Giải thích dòng gốc 321: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 323: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Giải phóng các cell quanh lưỡi cưa, queue debris ECS và đánh dấu chunk liên quan cần rebuild.</summary>
    // Giải thích dòng gốc 324: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
    public bool ReleaseAtWorld(Vector3 worldPoint, Vector3 pressDirection, float pressSpeed, float outwardForce,
        // Giải thích dòng gốc 325: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
        float tangentialForce, float spinDirection, float bladeRadius, float sideDamping, float maxVelocity)
    // Giải thích dòng gốc 326: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 327: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (!_cellSolid.IsCreated || _runtimeParent == null)
            // Giải thích dòng gốc 328: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return false;

        // Giải thích dòng gốc 330: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        QueueSawPush(worldPoint, pressDirection, pressSpeed, outwardForce, tangentialForce, spinDirection,
            // Giải thích dòng gốc 331: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
            bladeRadius, maxVelocity);

        // Giải thích dòng gốc 333: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        Vector3 localPoint = _runtimeParent.InverseTransformPoint(worldPoint);
        // Giải thích dòng gốc 334: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        float radiusSqr = _sawReleaseRadius * _sawReleaseRadius;
        // Giải thích dòng gốc 335: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int centerX = Mathf.RoundToInt((localPoint.x - _offset.x) / _cellSize);
        // Giải thích dòng gốc 336: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int centerY = Mathf.RoundToInt((localPoint.y - _offset.y) / _cellSize);
        // Giải thích dòng gốc 337: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int radiusCells = Mathf.Max(1, Mathf.CeilToInt(_sawReleaseRadius / _cellSize));
        // Giải thích dòng gốc 338: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int minX = Mathf.Max(0, centerX - radiusCells);
        // Giải thích dòng gốc 339: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int maxX = Mathf.Min(_gridWidth - 1, centerX + radiusCells);
        // Giải thích dòng gốc 340: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int minY = Mathf.Max(0, centerY - radiusCells);
        // Giải thích dòng gốc 341: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int maxY = Mathf.Min(_gridHeight - 1, centerY + radiusCells);

        // Giải thích dòng gốc 343: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        bool releasedAny = false;

        // Giải thích dòng gốc 345: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int y = minY; y <= maxY; y++)
        // Giải thích dòng gốc 346: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 347: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
            for (int x = minX; x <= maxX; x++)
            // Giải thích dòng gốc 348: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
            {
                // Giải thích dòng gốc 349: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                Vector3 cellLocal = GetCellLocalPosition(x, y);
                // Giải thích dòng gốc 350: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                Vector3 delta = cellLocal - localPoint;
                // Giải thích dòng gốc 351: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
                if (delta.x * delta.x + delta.y * delta.y > radiusSqr)
                    // Giải thích dòng gốc 352: Bỏ qua phần còn lại của lần lặp hiện tại và sang lần tiếp theo.
                    continue;

                // Giải thích dòng gốc 354: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                int cellIndex = y * _gridWidth + x;
                // Giải thích dòng gốc 355: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
                if (_cellSolid[cellIndex] == 0)
                    // Giải thích dòng gốc 356: Bỏ qua phần còn lại của lần lặp hiện tại và sang lần tiếp theo.
                    continue;

                // Giải thích dòng gốc 358: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                Color32 color = _cellColors[cellIndex];
                // Giải thích dòng gốc 359: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                _cellSolid[cellIndex] = 0;
                // Giải thích dòng gốc 360: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
                if (TryConsumePhysicsDebrisBudget())
                // Giải thích dòng gốc 361: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
                {
                    // Giải thích dòng gốc 362: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                    QueueReleasedBlockSpawn(cellLocal, color, worldPoint, pressDirection, pressSpeed, outwardForce,
                        // Giải thích dòng gốc 363: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                        tangentialForce, spinDirection, bladeRadius, sideDamping, maxVelocity);
                // Giải thích dòng gốc 364: Đóng khối code hiện tại.
                }

                // Giải thích dòng gốc 366: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                MarkCellChunkDirty(x, y);
                // Giải thích dòng gốc 367: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                releasedAny = true;
            // Giải thích dòng gốc 368: Đóng khối code hiện tại.
            }
        // Giải thích dòng gốc 369: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 371: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
        return releasedAny;
    // Giải thích dòng gốc 372: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 374: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Chuyển điểm world-space sang cell grid rồi tìm cell đặc trong bán kính cho trước.</summary>
    // Giải thích dòng gốc 375: Khai báo hàm HasSolidAtWorld với tham số trong ngoặc để thực hiện một hành vi.
    public bool HasSolidAtWorld(Vector3 worldPoint, float radius)
    // Giải thích dòng gốc 376: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 377: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (!_cellSolid.IsCreated || _runtimeParent == null)
            // Giải thích dòng gốc 378: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return false;

        // Giải thích dòng gốc 380: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        Vector3 localPoint = _runtimeParent.InverseTransformPoint(worldPoint);
        // Giải thích dòng gốc 381: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        float radiusSqr = radius * radius;
        // Giải thích dòng gốc 382: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int centerX = Mathf.RoundToInt((localPoint.x - _offset.x) / _cellSize);
        // Giải thích dòng gốc 383: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int centerY = Mathf.RoundToInt((localPoint.y - _offset.y) / _cellSize);
        // Giải thích dòng gốc 384: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int radiusCells = Mathf.Max(1, Mathf.CeilToInt(radius / _cellSize));
        // Giải thích dòng gốc 385: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int minX = Mathf.Max(0, centerX - radiusCells);
        // Giải thích dòng gốc 386: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int maxX = Mathf.Min(_gridWidth - 1, centerX + radiusCells);
        // Giải thích dòng gốc 387: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int minY = Mathf.Max(0, centerY - radiusCells);
        // Giải thích dòng gốc 388: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int maxY = Mathf.Min(_gridHeight - 1, centerY + radiusCells);

        // Giải thích dòng gốc 390: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int y = minY; y <= maxY; y++)
        // Giải thích dòng gốc 391: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 392: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
            for (int x = minX; x <= maxX; x++)
            // Giải thích dòng gốc 393: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
            {
                // Giải thích dòng gốc 394: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                int cellIndex = y * _gridWidth + x;
                // Giải thích dòng gốc 395: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
                if (_cellSolid[cellIndex] == 0)
                    // Giải thích dòng gốc 396: Bỏ qua phần còn lại của lần lặp hiện tại và sang lần tiếp theo.
                    continue;

                // Giải thích dòng gốc 398: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                Vector3 cellLocal = GetCellLocalPosition(x, y);
                // Giải thích dòng gốc 399: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                Vector3 delta = cellLocal - localPoint;
                // Giải thích dòng gốc 400: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
                if (delta.x * delta.x + delta.y * delta.y <= radiusSqr)
                    // Giải thích dòng gốc 401: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                    return true;
            // Giải thích dòng gốc 402: Đóng khối code hiện tại.
            }
        // Giải thích dòng gốc 403: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 405: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
        return false;
    // Giải thích dòng gốc 406: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 408: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Đánh dấu chunk chứa cell vừa thay đổi để rebuild dần ở các frame sau.</summary>
    // Giải thích dòng gốc 409: Khai báo hàm MarkCellChunkDirty với tham số trong ngoặc để thực hiện một hành vi.
    private void MarkCellChunkDirty(int cellX, int cellY)
    // Giải thích dòng gốc 410: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 411: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_chunks.Count == 0 || _chunkDirty == null || _chunkColumns <= 0)
            // Giải thích dòng gốc 412: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return;

        // Giải thích dòng gốc 414: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int chunkX = cellX / _chunkSize;
        // Giải thích dòng gốc 415: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int chunkY = cellY / _chunkSize;
        // Giải thích dòng gốc 416: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int chunkIndex = chunkY * _chunkColumns + chunkX;
        // Giải thích dòng gốc 417: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if ((uint)chunkIndex >= (uint)_chunks.Count || _chunkDirty[chunkIndex] != 0)
            // Giải thích dòng gốc 418: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return;

        // Giải thích dòng gốc 420: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _chunkDirty[chunkIndex] = 1;
        // Giải thích dòng gốc 421: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        _dirtyChunks.Add(chunkIndex);
    // Giải thích dòng gốc 422: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 424: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Kiểm tra và trừ quota spawn debris trong frame hiện tại.</summary>
    // Giải thích dòng gốc 425: Khai báo hàm TryConsumePhysicsDebrisBudget với tham số trong ngoặc để thực hiện một hành vi.
    private bool TryConsumePhysicsDebrisBudget()
    // Giải thích dòng gốc 426: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 427: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_maxPhysicsDebrisPerFrame == 0)
            // Giải thích dòng gốc 428: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return true;

        // Giải thích dòng gốc 430: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int frame = Time.frameCount;
        // Giải thích dòng gốc 431: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_physicsDebrisFrame != frame)
        // Giải thích dòng gốc 432: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 433: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _physicsDebrisFrame = frame;
            // Giải thích dòng gốc 434: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _physicsDebrisSpawnedThisFrame = 0;
        // Giải thích dòng gốc 435: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 437: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_physicsDebrisSpawnedThisFrame >= _maxPhysicsDebrisPerFrame)
            // Giải thích dòng gốc 438: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return false;

        // Giải thích dòng gốc 440: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
        _physicsDebrisSpawnedThisFrame++;
        // Giải thích dòng gốc 441: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
        return true;
    // Giải thích dòng gốc 442: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 444: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Chia cell grid thành các GameObject chunk và tạo runtime data tương ứng.</summary>
    // Giải thích dòng gốc 445: Khai báo hàm CreateChunks với tham số trong ngoặc để thực hiện một hành vi.
    private void CreateChunks()
    // Giải thích dòng gốc 446: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 447: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        RenderMaterial material = _runtimeChunkMaterial;
        // Giải thích dòng gốc 448: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _chunkColumns = Mathf.CeilToInt(_gridWidth / (float)_chunkSize);
        // Giải thích dòng gốc 449: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int chunkRows = Mathf.CeilToInt(_gridHeight / (float)_chunkSize);
        // Giải thích dòng gốc 450: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int chunkCount = _chunkColumns * chunkRows;
        // Giải thích dòng gốc 451: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _chunkDirty = new byte[chunkCount];

        // Giải thích dòng gốc 453: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int chunkY = 0; chunkY < chunkRows; chunkY++)
        // Giải thích dòng gốc 454: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 455: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
            for (int chunkX = 0; chunkX < _chunkColumns; chunkX++)
            // Giải thích dòng gốc 456: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
            {
                // Giải thích dòng gốc 457: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                int startX = chunkX * _chunkSize;
                // Giải thích dòng gốc 458: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                int startY = chunkY * _chunkSize;
                // Giải thích dòng gốc 459: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                int width = Mathf.Min(_chunkSize, _gridWidth - startX);
                // Giải thích dòng gốc 460: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                int height = Mathf.Min(_chunkSize, _gridHeight - startY);

                // Giải thích dòng gốc 462: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                GameObject chunkObject = new GameObject("TextureChunk_" + chunkX + "_" + chunkY);
                // Giải thích dòng gốc 463: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                chunkObject.transform.SetParent(_runtimeParent, false);

                // Giải thích dòng gốc 465: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                MeshFilter meshFilter = chunkObject.AddComponent<MeshFilter>();
                // Giải thích dòng gốc 466: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                MeshRenderer meshRenderer = chunkObject.AddComponent<MeshRenderer>();
                // Giải thích dòng gốc 467: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                TextureBlockChunk chunk = chunkObject.AddComponent<TextureBlockChunk>();
                // Giải thích dòng gốc 468: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                Mesh mesh = new Mesh { name = "TextureChunk_Mesh_" + chunkX + "_" + chunkY };
                // Giải thích dòng gốc 469: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                mesh.MarkDynamic();

                // Giải thích dòng gốc 471: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                meshFilter.sharedMesh = mesh;
                // Giải thích dòng gốc 472: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                meshRenderer.sharedMaterial = material;
                // Giải thích dòng gốc 473: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                chunk.Initialize(this);

                // Giải thích dòng gốc 475: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                _chunks.Add(CreateChunkRuntime(mesh, meshRenderer, startX, startY, width, height,
                    // Giải thích dòng gốc 476: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                    _renderVoxelDetailFromStart));
            // Giải thích dòng gốc 477: Đóng khối code hiện tại.
            }
        // Giải thích dòng gốc 478: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 480: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        _scheduledChunkRebuilds.Clear();
        // Giải thích dòng gốc 481: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int i = 0; i < _chunks.Count; i++)
            // Giải thích dòng gốc 482: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _scheduledChunkRebuilds.Add(i);

        // Giải thích dòng gốc 484: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        ScheduleAndApplyChunkRebuilds();
    // Giải thích dòng gốc 485: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 487: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Khởi tạo native buffer, mesh và metadata cho một chunk grid.</summary>
    // Giải thích dòng gốc 488: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
    private ChunkRuntime CreateChunkRuntime(Mesh mesh, MeshRenderer renderer, int startX, int startY, int width,
        // Giải thích dòng gốc 489: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
        int height, bool isDetailed)
    // Giải thích dòng gốc 490: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 491: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int maxCells = width * height;
        // Giải thích dòng gốc 492: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int vertexCapacity = isDetailed ? maxCells * 24 : maxCells * 4;
        // Giải thích dòng gốc 493: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int indexCapacity = isDetailed ? maxCells * 36 : maxCells * 6;

        // Giải thích dòng gốc 495: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
        return new ChunkRuntime
        // Giải thích dòng gốc 496: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 497: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Mesh = mesh,
            // Giải thích dòng gốc 498: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Renderer = renderer,
            // Giải thích dòng gốc 499: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            StartX = startX,
            // Giải thích dòng gốc 500: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            StartY = startY,
            // Giải thích dòng gốc 501: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Width = width,
            // Giải thích dòng gốc 502: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Height = height,
            // Giải thích dòng gốc 503: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            IsDetailed = isDetailed,
            // Giải thích dòng gốc 504: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Visited = new NativeArray<byte>(maxCells, Allocator.Persistent),
            // Giải thích dòng gốc 505: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Vertices = new NativeList<Vector3>(vertexCapacity, Allocator.Persistent),
            // Giải thích dòng gốc 506: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Colors = new NativeList<Color32>(vertexCapacity, Allocator.Persistent),
            // Giải thích dòng gốc 507: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Uvs = new NativeList<Vector2>(vertexCapacity, Allocator.Persistent),
            // Giải thích dòng gốc 508: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Indices = new NativeList<int>(indexCapacity, Allocator.Persistent),
            // Giải thích dòng gốc 509: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            ColliderVisited = new NativeArray<byte>(maxCells, Allocator.Persistent),
            // Giải thích dòng gốc 510: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            ColliderVertices = new NativeList<Vector3>(maxCells * 24, Allocator.Persistent),
            // Giải thích dòng gốc 511: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            ColliderColors = new NativeList<Color32>(maxCells * 24, Allocator.Persistent),
            // Giải thích dòng gốc 512: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            ColliderUvs = new NativeList<Vector2>(maxCells * 24, Allocator.Persistent),
            // Giải thích dòng gốc 513: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            ColliderIndices = new NativeList<int>(maxCells * 36, Allocator.Persistent)
        // Giải thích dòng gốc 514: Đóng khối khởi tạo dữ liệu và kết thúc câu lệnh.
        };
    // Giải thích dòng gốc 515: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 517: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Schedule job rebuild cho chunk bẩn và áp dụng kết quả đã hoàn thành theo frame budget.</summary>
    // Giải thích dòng gốc 518: Khai báo hàm ScheduleAndApplyChunkRebuilds với tham số trong ngoặc để thực hiện một hành vi.
    private void ScheduleAndApplyChunkRebuilds()
    // Giải thích dòng gốc 519: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 520: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_scheduledChunkRebuilds.Count == 0)
            // Giải thích dòng gốc 521: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return;

        // Giải thích dòng gốc 523: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        _scheduledChunkHandles.Clear();

        // Giải thích dòng gốc 525: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int i = 0; i < _scheduledChunkRebuilds.Count; i++)
        // Giải thích dòng gốc 526: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 527: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            int chunkIndex = _scheduledChunkRebuilds[i];
            // Giải thích dòng gốc 528: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if ((uint)chunkIndex >= (uint)_chunks.Count)
                // Giải thích dòng gốc 529: Bỏ qua phần còn lại của lần lặp hiện tại và sang lần tiếp theo.
                continue;

            // Giải thích dòng gốc 531: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            ChunkRuntime chunk = _chunks[chunkIndex];
            // Giải thích dòng gốc 532: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            chunk.Vertices.Clear();
            // Giải thích dòng gốc 533: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            chunk.Colors.Clear();
            // Giải thích dòng gốc 534: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            chunk.Uvs.Clear();
            // Giải thích dòng gốc 535: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            chunk.Indices.Clear();
            // Giải thích dòng gốc 536: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            chunk.ColliderVertices.Clear();
            // Giải thích dòng gốc 537: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            chunk.ColliderColors.Clear();
            // Giải thích dòng gốc 538: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            chunk.ColliderUvs.Clear();
            // Giải thích dòng gốc 539: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            chunk.ColliderIndices.Clear();

            // Giải thích dòng gốc 541: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            BuildChunkMeshJob meshJob = new BuildChunkMeshJob
            // Giải thích dòng gốc 542: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
            {
                // Giải thích dòng gốc 543: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                CellColors = _cellColors,
                // Giải thích dòng gốc 544: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                CellSolid = _cellSolid,
                // Giải thích dòng gốc 545: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                Visited = chunk.Visited,
                // Giải thích dòng gốc 546: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                Vertices = chunk.Vertices,
                // Giải thích dòng gốc 547: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                Colors = chunk.Colors,
                // Giải thích dòng gốc 548: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                Uvs = chunk.Uvs,
                // Giải thích dòng gốc 549: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                Indices = chunk.Indices,
                // Giải thích dòng gốc 550: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                GridWidth = _gridWidth,
                // Giải thích dòng gốc 551: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                StartX = chunk.StartX,
                // Giải thích dòng gốc 552: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                StartY = chunk.StartY,
                // Giải thích dòng gốc 553: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                ChunkWidth = chunk.Width,
                // Giải thích dòng gốc 554: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                ChunkHeight = chunk.Height,
                // Giải thích dòng gốc 555: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                CellSize = _cellSize,
                // Giải thích dòng gốc 556: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                Offset = _offset,
                // Giải thích dòng gốc 557: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                UseVoxelDetail = chunk.IsDetailed ? (byte)1 : (byte)0,
                // Giải thích dòng gốc 558: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                ExtrudeMergedQuads = 0,
                // Giải thích dòng gốc 559: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                MergeAnySolid = 0,
                // Giải thích dòng gốc 560: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                DetailVoxelScale = _detailVoxelScale,
                // Giải thích dòng gốc 561: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                RandomYRotation = _voxelRandomYRotation,
                // Giải thích dòng gốc 562: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                DetailDepth = _chunkColliderDepth
            // Giải thích dòng gốc 563: Đóng khối khởi tạo dữ liệu và kết thúc câu lệnh.
            };

            // Giải thích dòng gốc 565: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _scheduledChunkHandles.Add(meshJob.Schedule());

            // Giải thích dòng gốc 567: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            BuildChunkMeshJob colliderJob = new BuildChunkMeshJob
            // Giải thích dòng gốc 568: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
            {
                // Giải thích dòng gốc 569: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                CellColors = _cellColors,
                // Giải thích dòng gốc 570: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                CellSolid = _cellSolid,
                // Giải thích dòng gốc 571: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                Visited = chunk.ColliderVisited,
                // Giải thích dòng gốc 572: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                Vertices = chunk.ColliderVertices,
                // Giải thích dòng gốc 573: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                Colors = chunk.ColliderColors,
                // Giải thích dòng gốc 574: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                Uvs = chunk.ColliderUvs,
                // Giải thích dòng gốc 575: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                Indices = chunk.ColliderIndices,
                // Giải thích dòng gốc 576: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                GridWidth = _gridWidth,
                // Giải thích dòng gốc 577: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                StartX = chunk.StartX,
                // Giải thích dòng gốc 578: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                StartY = chunk.StartY,
                // Giải thích dòng gốc 579: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                ChunkWidth = chunk.Width,
                // Giải thích dòng gốc 580: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                ChunkHeight = chunk.Height,
                // Giải thích dòng gốc 581: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                CellSize = _cellSize,
                // Giải thích dòng gốc 582: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                Offset = _offset,
                // Giải thích dòng gốc 583: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                UseVoxelDetail = 0,
                // Giải thích dòng gốc 584: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                ExtrudeMergedQuads = 1,
                // Giải thích dòng gốc 585: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                MergeAnySolid = 1,
                // Giải thích dòng gốc 586: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                DetailVoxelScale = _detailVoxelScale,
                // Giải thích dòng gốc 587: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                RandomYRotation = 0f,
                // Giải thích dòng gốc 588: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                DetailDepth = _chunkColliderDepth
            // Giải thích dòng gốc 589: Đóng khối khởi tạo dữ liệu và kết thúc câu lệnh.
            };

            // Giải thích dòng gốc 591: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _scheduledChunkHandles.Add(colliderJob.Schedule());
        // Giải thích dòng gốc 592: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 594: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        JobHandle combinedHandle = default;
        // Giải thích dòng gốc 595: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int i = 0; i < _scheduledChunkHandles.Count; i++)
            // Giải thích dòng gốc 596: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            combinedHandle = JobHandle.CombineDependencies(combinedHandle, _scheduledChunkHandles[i]);

        // Giải thích dòng gốc 598: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        combinedHandle.Complete();

        // Giải thích dòng gốc 600: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int i = 0; i < _scheduledChunkRebuilds.Count; i++)
        // Giải thích dòng gốc 601: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 602: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            int chunkIndex = _scheduledChunkRebuilds[i];
            // Giải thích dòng gốc 603: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if ((uint)chunkIndex >= (uint)_chunks.Count)
                // Giải thích dòng gốc 604: Bỏ qua phần còn lại của lần lặp hiện tại và sang lần tiếp theo.
                continue;

            // Giải thích dòng gốc 606: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            ChunkRuntime chunk = _chunks[chunkIndex];
            // Giải thích dòng gốc 607: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            ApplyChunkMesh(ref chunk);
            // Giải thích dòng gốc 608: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _chunks[chunkIndex] = chunk;
        // Giải thích dòng gốc 609: Đóng khối code hiện tại.
        }
    // Giải thích dòng gốc 610: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 612: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Đổ vertex/color/UV/index từ native buffer vào Unity Mesh của chunk.</summary>
    // Giải thích dòng gốc 613: Khai báo hàm ApplyChunkMesh với tham số trong ngoặc để thực hiện một hành vi.
    private void ApplyChunkMesh(ref ChunkRuntime chunk)
    // Giải thích dòng gốc 614: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 615: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        Mesh mesh = chunk.Mesh;
        // Giải thích dòng gốc 616: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        mesh.Clear();

        // Giải thích dòng gốc 618: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (chunk.Vertices.Length > 0)
        // Giải thích dòng gốc 619: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 620: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            mesh.indexFormat = chunk.Vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            // Giải thích dòng gốc 621: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            mesh.SetVertices(chunk.Vertices.AsArray());
            // Giải thích dòng gốc 622: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            mesh.SetColors(chunk.Colors.AsArray());
            // Giải thích dòng gốc 623: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            mesh.SetUVs(0, chunk.Uvs.AsArray());
            // Giải thích dòng gốc 624: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            mesh.SetIndices(chunk.Indices.AsArray(), MeshTopology.Triangles, 0);
            // Giải thích dòng gốc 625: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (chunk.IsDetailed)
                // Giải thích dòng gốc 626: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                mesh.RecalculateNormals();
            // Giải thích dòng gốc 627: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            mesh.RecalculateBounds();
            // Giải thích dòng gốc 628: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            chunk.Renderer.enabled = true;
            // Giải thích dòng gốc 629: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            ApplyChunkPhysicsCollider(ref chunk);
        // Giải thích dòng gốc 630: Đóng khối code hiện tại.
        }
        // Giải thích dòng gốc 631: Chạy nhánh thay thế khi điều kiện if trước đó không đúng.
        else
        // Giải thích dòng gốc 632: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 633: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            chunk.Renderer.enabled = false;
            // Giải thích dòng gốc 634: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            DestroyChunkPhysicsCollider(ref chunk);
        // Giải thích dòng gốc 635: Đóng khối code hiện tại.
        }
    // Giải thích dòng gốc 636: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 638: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Tạo hoặc thay collider Unity Physics cho vùng cell đặc trong chunk.</summary>
    // Giải thích dòng gốc 639: Khai báo hàm ApplyChunkPhysicsCollider với tham số trong ngoặc để thực hiện một hành vi.
    private void ApplyChunkPhysicsCollider(ref ChunkRuntime chunk)
    // Giải thích dòng gốc 640: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 641: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (!EnsureEcsReady())
            // Giải thích dòng gốc 642: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return;

        // Giải thích dòng gốc 644: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (chunk.ColliderVertices.Length == 0 || chunk.ColliderIndices.Length == 0)
        // Giải thích dòng gốc 645: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 646: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            DestroyChunkPhysicsCollider(ref chunk);
            // Giải thích dòng gốc 647: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return;
        // Giải thích dòng gốc 648: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 650: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int triangleCount = chunk.ColliderIndices.Length / 3;
        // Giải thích dòng gốc 651: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        NativeArray<float3> vertices = new NativeArray<float3>(chunk.ColliderVertices.Length, Allocator.Temp);
        // Giải thích dòng gốc 652: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        NativeArray<int3> triangles = new NativeArray<int3>(triangleCount, Allocator.Temp);

        // Giải thích dòng gốc 654: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int i = 0; i < chunk.ColliderVertices.Length; i++)
        // Giải thích dòng gốc 655: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 656: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 world = _runtimeParent.TransformPoint(chunk.ColliderVertices[i]);
            // Giải thích dòng gốc 657: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            vertices[i] = new float3(world.x, world.y, world.z);
        // Giải thích dòng gốc 658: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 660: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int i = 0; i < triangleCount; i++)
        // Giải thích dòng gốc 661: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 662: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            int index = i * 3;
            // Giải thích dòng gốc 663: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            triangles[i] = new int3(chunk.ColliderIndices[index], chunk.ColliderIndices[index + 1],
                // Giải thích dòng gốc 664: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                chunk.ColliderIndices[index + 2]);
        // Giải thích dòng gốc 665: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 667: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        PhysicsMaterial physicsMaterial = CreatePhysicsMaterial();
        // Giải thích dòng gốc 668: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        BlobAssetReference<Collider> collider = Unity.Physics.MeshCollider.Create(vertices, triangles,
            // Giải thích dòng gốc 669: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
            CollisionFilter.Default, physicsMaterial);
        // Giải thích dòng gốc 670: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        vertices.Dispose();
        // Giải thích dòng gốc 671: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        triangles.Dispose();

        // Giải thích dòng gốc 673: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (chunk.PhysicsCollider.IsCreated)
            // Giải thích dòng gốc 674: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            chunk.PhysicsCollider.Dispose();
        // Giải thích dòng gốc 675: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        chunk.PhysicsCollider = collider;

        // Giải thích dòng gốc 677: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (chunk.PhysicsEntity == Entity.Null || !_entityManager.Exists(chunk.PhysicsEntity))
        // Giải thích dòng gốc 678: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 679: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            chunk.PhysicsEntity = _entityManager.CreateEntity(typeof(LocalTransform), typeof(PhysicsCollider));
            // Giải thích dòng gốc 680: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _entityManager.SetComponentData(chunk.PhysicsEntity,
                // Giải thích dòng gốc 681: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));
            // Giải thích dòng gốc 682: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _entityManager.AddSharedComponent(chunk.PhysicsEntity, new PhysicsWorldIndex(0));
        // Giải thích dòng gốc 683: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 685: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _entityManager.SetComponentData(chunk.PhysicsEntity, new PhysicsCollider { Value = chunk.PhysicsCollider });
    // Giải thích dòng gốc 686: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 688: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Hủy entity/collider physics cũ trước khi rebuild chunk hoặc dispose.</summary>
    // Giải thích dòng gốc 689: Khai báo hàm DestroyChunkPhysicsCollider với tham số trong ngoặc để thực hiện một hành vi.
    private void DestroyChunkPhysicsCollider(ref ChunkRuntime chunk)
    // Giải thích dòng gốc 690: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 691: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (EnsureEcsReady() && chunk.PhysicsEntity != Entity.Null && _entityManager.Exists(chunk.PhysicsEntity))
            // Giải thích dòng gốc 692: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _entityManager.DestroyEntity(chunk.PhysicsEntity);

        // Giải thích dòng gốc 694: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        chunk.PhysicsEntity = Entity.Null;
        // Giải thích dòng gốc 695: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (chunk.PhysicsCollider.IsCreated)
        // Giải thích dòng gốc 696: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 697: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            chunk.PhysicsCollider.Dispose();
            // Giải thích dòng gốc 698: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            chunk.PhysicsCollider = default;
        // Giải thích dòng gốc 699: Đóng khối code hiện tại.
        }
    // Giải thích dòng gốc 700: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 702: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Complete job còn treo rồi dispose toàn bộ native buffer, mesh và chunk object.</summary>
    // Giải thích dòng gốc 703: Khai báo hàm DisposeChunks với tham số trong ngoặc để thực hiện một hành vi.
    private void DisposeChunks()
    // Giải thích dòng gốc 704: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 705: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int i = 0; i < _chunks.Count; i++)
        // Giải thích dòng gốc 706: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 707: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            ChunkRuntime chunk = _chunks[i];
            // Giải thích dòng gốc 708: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (chunk.Visited.IsCreated)
                // Giải thích dòng gốc 709: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                chunk.Visited.Dispose();
            // Giải thích dòng gốc 710: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (chunk.Vertices.IsCreated)
                // Giải thích dòng gốc 711: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                chunk.Vertices.Dispose();
            // Giải thích dòng gốc 712: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (chunk.Colors.IsCreated)
                // Giải thích dòng gốc 713: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                chunk.Colors.Dispose();
            // Giải thích dòng gốc 714: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (chunk.Uvs.IsCreated)
                // Giải thích dòng gốc 715: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                chunk.Uvs.Dispose();
            // Giải thích dòng gốc 716: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (chunk.Indices.IsCreated)
                // Giải thích dòng gốc 717: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                chunk.Indices.Dispose();
            // Giải thích dòng gốc 718: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (chunk.ColliderVisited.IsCreated)
                // Giải thích dòng gốc 719: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                chunk.ColliderVisited.Dispose();
            // Giải thích dòng gốc 720: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (chunk.ColliderVertices.IsCreated)
                // Giải thích dòng gốc 721: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                chunk.ColliderVertices.Dispose();
            // Giải thích dòng gốc 722: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (chunk.ColliderColors.IsCreated)
                // Giải thích dòng gốc 723: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                chunk.ColliderColors.Dispose();
            // Giải thích dòng gốc 724: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (chunk.ColliderUvs.IsCreated)
                // Giải thích dòng gốc 725: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                chunk.ColliderUvs.Dispose();
            // Giải thích dòng gốc 726: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (chunk.ColliderIndices.IsCreated)
                // Giải thích dòng gốc 727: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                chunk.ColliderIndices.Dispose();
            // Giải thích dòng gốc 728: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            DestroyChunkPhysicsCollider(ref chunk);
            // Giải thích dòng gốc 729: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (chunk.Mesh != null)
                // Giải thích dòng gốc 730: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                DestroyUnityObject(chunk.Mesh);
        // Giải thích dòng gốc 731: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 733: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        _chunks.Clear();
    // Giải thích dòng gốc 734: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 736: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Trả về material runtime dùng cho chunk, tạo bản copy khi cần tránh sửa asset gốc.</summary>
    // Giải thích dòng gốc 737: Khai báo hàm ResolveChunkMaterial với tham số trong ngoặc để thực hiện một hành vi.
    private RenderMaterial ResolveChunkMaterial()
    // Giải thích dòng gốc 738: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 739: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_chunkMaterial != null)
            // Giải thích dòng gốc 740: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return _chunkMaterial;

        // Giải thích dòng gốc 742: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        Shader voxelShader = Shader.Find("BlockCrusher/VoxelExactColor");
        // Giải thích dòng gốc 743: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (voxelShader != null)
            // Giải thích dòng gốc 744: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return new RenderMaterial(voxelShader);

        // Giải thích dòng gốc 746: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        Shader shader = Shader.Find("Sprites/Default");
        // Giải thích dòng gốc 747: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (shader != null)
            // Giải thích dòng gốc 748: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return new RenderMaterial(shader);

        // Giải thích dòng gốc 750: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
        return null;
    // Giải thích dòng gốc 751: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 753: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Đổi chỉ số cell sang vị trí local tại tâm cell.</summary>
    // Giải thích dòng gốc 754: Khai báo hàm GetCellLocalPosition với tham số trong ngoặc để thực hiện một hành vi.
    private Vector3 GetCellLocalPosition(int x, int y)
    // Giải thích dòng gốc 755: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 756: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
        return _offset + new Vector3(x * _cellSize, y * _cellSize, 0f);
    // Giải thích dòng gốc 757: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 759: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Tạo entity debris ECS cho cell vừa tách và gán vận tốc ban đầu từ chuyển động cưa.</summary>
    // Giải thích dòng gốc 760: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
    private void QueueReleasedBlockSpawn(Vector3 localPosition, Color32 color, Vector3 sawCenter, Vector3 pressDirection,
        // Giải thích dòng gốc 761: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
        float pressSpeed, float outwardForce, float tangentialForce, float spinDirection, float bladeRadius,
        // Giải thích dòng gốc 762: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
        float sideDamping, float maxVelocity)
    // Giải thích dòng gốc 763: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 764: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (!EnsureReleasedBlockResources() || !EnsureEcsReady())
            // Giải thích dòng gốc 765: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return;

        // Giải thích dòng gốc 767: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        Vector3 position = _runtimeParent.TransformPoint(localPosition);
        // Giải thích dòng gốc 768: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        Vector3 outward = position - sawCenter;
        // Giải thích dòng gốc 769: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        outward.z = 0f;
        // Giải thích dòng gốc 770: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        float distance = outward.magnitude;
        // Giải thích dòng gốc 771: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (distance <= 0.0001f)
            // Giải thích dòng gốc 772: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            outward = pressDirection.sqrMagnitude > 0.0001f ? pressDirection : Vector3.up;
        // Giải thích dòng gốc 773: Chạy nhánh thay thế khi điều kiện if trước đó không đúng.
        else
            // Giải thích dòng gốc 774: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            outward.Normalize();

        // Giải thích dòng gốc 776: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        float radiusPush = bladeRadius > 0f ? Mathf.Clamp01((bladeRadius - distance) / bladeRadius) : 0f;
        // Giải thích dòng gốc 777: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        Vector3 tangent = new Vector3(-outward.y, outward.x, 0f) * Mathf.Sign(spinDirection);
        // Giải thích dòng gốc 778: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        float speedScale = 1f + Mathf.Min(pressSpeed, 4f) * 0.1f;
        // Giải thích dòng gốc 779: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        float pushScale = 1f + radiusPush * 1.25f;
        // Giải thích dòng gốc 780: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        Vector3 velocity = outward * (outwardForce * 0.08f * pushScale * speedScale) +
                           // Giải thích dòng gốc 781: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                           (Vector3.up + tangent * 0.1f).normalized *
                           // Giải thích dòng gốc 782: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                           (tangentialForce * 0.21f * pushScale * speedScale * radiusPush);
        // Giải thích dòng gốc 783: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        velocity -= outward * Vector3.Dot(velocity, outward) * sideDamping * radiusPush * 0.15f;
        // Giải thích dòng gốc 784: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        float safeMaxVelocity = GetSafePhysicsVelocity(maxVelocity);
        // Giải thích dòng gốc 785: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        Vector3 clampedVelocity = Vector3.ClampMagnitude(velocity, safeMaxVelocity);
        // Giải thích dòng gốc 786: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        clampedVelocity = RedirectVelocityFromSolid(position, clampedVelocity);

        // Giải thích dòng gốc 788: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_releasedBlockEntities.Count >= _maxReleasedPhysicsBlocks)
            // Giải thích dòng gốc 789: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            TrimReleasedBlockEntityList();
        // Giải thích dòng gốc 790: Bắt đầu vòng lặp while và tiếp tục lặp khi điều kiện còn đúng.
        while (_releasedBlockEntities.Count >= _maxReleasedPhysicsBlocks && _releasedBlockEntities.Count > 0)
            // Giải thích dòng gốc 791: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            DestroyReleasedBlockEntityAt(0);

        // Giải thích dòng gốc 793: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        Entity entity = _entityManager.CreateEntity(_releasedBlockArchetype);

        // Giải thích dòng gốc 795: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(
            // Giải thích dòng gốc 796: Tạo một instance mới của kiểu dữ liệu được chỉ định.
            new float3(position.x, position.y, position.z), quaternion.identity, _releasedBlockScale));
        // Giải thích dòng gốc 797: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _entityManager.SetComponentData(entity, new PhysicsCollider { Value = _releasedBlockCollider });
        // Giải thích dòng gốc 798: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        PhysicsMass physicsMass = PhysicsMass.CreateDynamic(
            // Giải thích dòng gốc 799: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
            _releasedBlockCollider.Value.MassProperties, _releasedBlockMass);
        // Giải thích dòng gốc 800: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        physicsMass.InverseInertia = float3.zero;
        // Giải thích dòng gốc 801: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        _entityManager.SetComponentData(entity, physicsMass);
        // Giải thích dòng gốc 802: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        _entityManager.SetComponentData(entity, new PhysicsVelocity
        // Giải thích dòng gốc 803: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 804: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Linear = new float3(clampedVelocity.x, clampedVelocity.y, clampedVelocity.z),
            // Giải thích dòng gốc 805: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Angular = float3.zero
        // Giải thích dòng gốc 806: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
        });
        // Giải thích dòng gốc 807: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        _entityManager.SetComponentData(entity, new PhysicsDamping
        // Giải thích dòng gốc 808: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 809: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Linear = _releasedBlockDamping,
            // Giải thích dòng gốc 810: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Angular = _releasedBlockAngularDamping
        // Giải thích dòng gốc 811: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
        });
        // Giải thích dòng gốc 812: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _entityManager.SetComponentData(entity, new PhysicsGravityFactor { Value = 1f });
        // Giải thích dòng gốc 813: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        _entityManager.SetComponentData(entity, new ReleasedBlockComponent
        // Giải thích dòng gốc 814: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 815: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            OwnerId = _ownerId,
            // Giải thích dòng gốc 816: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Color = ToFloat4(color),
            // Giải thích dòng gốc 817: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            LockedZ = position.z,
            // Giải thích dòng gốc 818: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            MaxPlanarSpeed = safeMaxVelocity
        // Giải thích dòng gốc 819: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
        });
        // Giải thích dòng gốc 820: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        _releasedBlockEntities.Add(entity);
    // Giải thích dòng gốc 821: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 823: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Đảm bảo mesh, material, archetype và collider template của debris ECS đã sẵn sàng.</summary>
    // Giải thích dòng gốc 824: Khai báo hàm EnsureReleasedBlockResources với tham số trong ngoặc để thực hiện một hành vi.
    private bool EnsureReleasedBlockResources()
    // Giải thích dòng gốc 825: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 826: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_releasedBlockCollider.IsCreated && _releasedBlockMesh != null && _releasedBlockMaterial != null)
        // Giải thích dòng gốc 827: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 828: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            EnsureRenderFrameResources();
            // Giải thích dòng gốc 829: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return true;
        // Giải thích dòng gốc 830: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 832: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        ReleasedBlockAuthoring authoring = ResolveReleasedBlockAuthoring();
        // Giải thích dòng gốc 833: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        GameObject prefab = authoring != null ? authoring.ReleasedBlockPrefab : null;
        // Giải thích dòng gốc 834: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (prefab == null)
            // Giải thích dòng gốc 835: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return false;

        // Giải thích dòng gốc 837: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        MeshFilter meshFilter = prefab.GetComponentInChildren<MeshFilter>();
        // Giải thích dòng gốc 838: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        Renderer meshRenderer = prefab.GetComponentInChildren<Renderer>();
        // Giải thích dòng gốc 839: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (meshFilter == null || meshFilter.sharedMesh == null || meshRenderer == null)
            // Giải thích dòng gốc 840: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return false;

        // Giải thích dòng gốc 842: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _releasedBlockMesh = meshFilter.sharedMesh;
        // Giải thích dòng gốc 843: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _releasedBlockMaterial = new RenderMaterial(meshRenderer.sharedMaterial != null
            // Giải thích dòng gốc 844: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
            ? meshRenderer.sharedMaterial
            // Giải thích dòng gốc 845: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
            : _runtimeChunkMaterial)
        // Giải thích dòng gốc 846: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 847: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            enableInstancing = true
        // Giải thích dòng gốc 848: Đóng khối khởi tạo dữ liệu và kết thúc câu lệnh.
        };
        // Giải thích dòng gốc 849: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _releasedBlockPropertyBlock ??= new MaterialPropertyBlock();
        // Giải thích dòng gốc 850: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _releasedBlockScale = Mathf.Max(0.0001f, authoring.Scale);

        // Giải thích dòng gốc 852: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        Vector3 size = Vector3.one;
        // Giải thích dòng gốc 853: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        Vector3 center = Vector3.zero;
        // Giải thích dòng gốc 854: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        UnityEngine.BoxCollider box = prefab.GetComponentInChildren<UnityEngine.BoxCollider>();
        // Giải thích dòng gốc 855: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (box != null)
        // Giải thích dòng gốc 856: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 857: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            size = Vector3.Scale(box.size, box.transform.lossyScale);
            // Giải thích dòng gốc 858: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            center = Vector3.Scale(box.center, box.transform.lossyScale);
        // Giải thích dòng gốc 859: Đóng khối code hiện tại.
        }
        // Giải thích dòng gốc 860: Chạy nhánh thay thế khi điều kiện if trước đó không đúng.
        else
        // Giải thích dòng gốc 861: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 862: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Bounds bounds = _releasedBlockMesh.bounds;
            // Giải thích dòng gốc 863: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            size = bounds.size;
            // Giải thích dòng gốc 864: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            center = bounds.center;
        // Giải thích dòng gốc 865: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 867: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        PhysicsMaterial physicsMaterial = CreatePhysicsMaterial();
        // Giải thích dòng gốc 868: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        float radius = Mathf.Min(size.x, size.y) * 0.48f;
        // Giải thích dòng gốc 869: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _releasedBlockCollider = Unity.Physics.SphereCollider.Create(new SphereGeometry
        // Giải thích dòng gốc 870: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 871: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Center = new float3(center.x, center.y, center.z),
            // Giải thích dòng gốc 872: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Radius = radius
        // Giải thích dòng gốc 873: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
        }, CollisionFilter.Default, physicsMaterial);

        // Giải thích dòng gốc 875: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        EnsureRenderFrameResources();
        // Giải thích dòng gốc 876: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
        return _releasedBlockCollider.IsCreated;
    // Giải thích dòng gốc 877: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 879: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Lấy authoring debris được gán sẵn hoặc tìm fallback trong scene.</summary>
    // Giải thích dòng gốc 880: Khai báo hàm ResolveReleasedBlockAuthoring với tham số trong ngoặc để thực hiện một hành vi.
    private ReleasedBlockAuthoring ResolveReleasedBlockAuthoring()
    // Giải thích dòng gốc 881: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 882: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_releasedBlockAuthoring == null)
            // Giải thích dòng gốc 883: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _releasedBlockAuthoring = GetComponent<ReleasedBlockAuthoring>();

        // Giải thích dòng gốc 885: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
        return _releasedBlockAuthoring;
    // Giải thích dòng gốc 886: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 888: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Lấy EntityManager và tạo query/archetype cần để quản lý debris của spawner này.</summary>
    // Giải thích dòng gốc 889: Khai báo hàm EnsureEcsReady với tham số trong ngoặc để thực hiện một hành vi.
    private bool EnsureEcsReady()
    // Giải thích dòng gốc 890: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 891: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        World world = World.DefaultGameObjectInjectionWorld;
        // Giải thích dòng gốc 892: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (world == null || !world.IsCreated)
            // Giải thích dòng gốc 893: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return false;

        // Giải thích dòng gốc 895: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_ecsWorld != world || !_hasReleasedBlockQuery)
        // Giải thích dòng gốc 896: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 897: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            DisposeReleasedBlockWalls();
            // Giải thích dòng gốc 898: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            DisposeEcsQuery();
            // Giải thích dòng gốc 899: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _ecsWorld = world;
            // Giải thích dòng gốc 900: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _entityManager = world.EntityManager;
            // Giải thích dòng gốc 901: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _releasedBlockArchetype = _entityManager.CreateArchetype(
                // Giải thích dòng gốc 902: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                typeof(LocalTransform), typeof(PhysicsCollider), typeof(PhysicsMass), typeof(PhysicsVelocity),
                // Giải thích dòng gốc 903: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                typeof(PhysicsDamping), typeof(PhysicsGravityFactor), typeof(Simulate),
                // Giải thích dòng gốc 904: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                typeof(ReleasedBlockComponent), typeof(PhysicsWorldIndex));
            // Giải thích dòng gốc 905: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _releasedBlockQuery = _entityManager.CreateEntityQuery(
                // Giải thích dòng gốc 906: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                ComponentType.ReadOnly<ReleasedBlockComponent>(),
                // Giải thích dòng gốc 907: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                ComponentType.ReadOnly<LocalTransform>(),
                // Giải thích dòng gốc 908: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                ComponentType.ReadWrite<PhysicsVelocity>());
            // Giải thích dòng gốc 909: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _hasReleasedBlockQuery = true;
            // Giải thích dòng gốc 910: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            EnsurePhysicsStep();
            // Giải thích dòng gốc 911: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            EnsureReleasedBlockWalls();
        // Giải thích dòng gốc 912: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 914: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
        return true;
    // Giải thích dòng gốc 915: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 917: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Đảm bảo world ECS có physics simulation system trước khi spawn collider/debris.</summary>
    // Giải thích dòng gốc 918: Khai báo hàm EnsurePhysicsStep với tham số trong ngoặc để thực hiện một hành vi.
    private void EnsurePhysicsStep()
    // Giải thích dòng gốc 919: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 920: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        EntityQuery query = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<PhysicsStep>());
        // Giải thích dòng gốc 921: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (query.IsEmptyIgnoreFilter)
        // Giải thích dòng gốc 922: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 923: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            PhysicsStep step = PhysicsStep.Default;
            // Giải thích dòng gốc 924: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            step.SubstepCount = 1;
            // Giải thích dòng gốc 925: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            step.SolverIterationCount = 4;
            // Giải thích dòng gốc 926: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            step.CollisionTolerance = Mathf.Max(step.CollisionTolerance, _cellSize * 0.1f);
            // Giải thích dòng gốc 927: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _entityManager.CreateSingleton(step, "Gameplay Physics Step");
        // Giải thích dòng gốc 928: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 930: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        query.Dispose();
    // Giải thích dòng gốc 931: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 933: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Tạo collider tường ECS từ các Transform giới hạn để debris không rơi ra ngoài.</summary>
    // Giải thích dòng gốc 934: Khai báo hàm EnsureReleasedBlockWalls với tham số trong ngoặc để thực hiện một hành vi.
    private void EnsureReleasedBlockWalls()
    // Giải thích dòng gốc 935: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 936: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_releasedBlockWallEntities.Count > 0 || _releasedBlockWalls == null)
            // Giải thích dòng gốc 937: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return;

        // Giải thích dòng gốc 939: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        PhysicsMaterial material = CreatePhysicsMaterial();
        // Giải thích dòng gốc 940: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int i = 0; i < _releasedBlockWalls.Length; i++)
        // Giải thích dòng gốc 941: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 942: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Transform wall = _releasedBlockWalls[i];
            // Giải thích dòng gốc 943: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (wall == null || !wall.gameObject.activeInHierarchy)
                // Giải thích dòng gốc 944: Bỏ qua phần còn lại của lần lặp hiện tại và sang lần tiếp theo.
                continue;

            // Giải thích dòng gốc 946: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 scale = wall.lossyScale;
            // Giải thích dòng gốc 947: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 size = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            // Giải thích dòng gốc 948: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            BlobAssetReference<Collider> collider = Unity.Physics.BoxCollider.Create(new BoxGeometry
            // Giải thích dòng gốc 949: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
            {
                // Giải thích dòng gốc 950: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                Center = float3.zero,
                // Giải thích dòng gốc 951: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                Size = new float3(size.x, size.y, size.z),
                // Giải thích dòng gốc 952: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                Orientation = quaternion.identity,
                // Giải thích dòng gốc 953: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                BevelRadius = 0f
            // Giải thích dòng gốc 954: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
            }, CollisionFilter.Default, material);

            // Giải thích dòng gốc 956: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 center = wall.position;
            // Giải thích dòng gốc 957: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Quaternion rotation = wall.rotation;
            // Giải thích dòng gốc 958: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Entity entity = _entityManager.CreateEntity(typeof(LocalTransform), typeof(PhysicsCollider));
            // Giải thích dòng gốc 959: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _entityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(
                // Giải thích dòng gốc 960: Tạo một instance mới của kiểu dữ liệu được chỉ định.
                new float3(center.x, center.y, center.z),
                // Giải thích dòng gốc 961: Tạo một instance mới của kiểu dữ liệu được chỉ định.
                new quaternion(rotation.x, rotation.y, rotation.z, rotation.w), 1f));
            // Giải thích dòng gốc 962: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _entityManager.SetComponentData(entity, new PhysicsCollider { Value = collider });
            // Giải thích dòng gốc 963: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _entityManager.AddSharedComponent(entity, new PhysicsWorldIndex(0));

            // Giải thích dòng gốc 965: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _releasedBlockWallColliders.Add(collider);
            // Giải thích dòng gốc 966: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _releasedBlockWallEntities.Add(entity);
        // Giải thích dòng gốc 967: Đóng khối code hiện tại.
        }
    // Giải thích dòng gốc 968: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 970: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Áp lực cưa lên debris cùng owner, giới hạn vận tốc và giữ chuyển động trong mặt phẳng gameplay.</summary>
    // Giải thích dòng gốc 971: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
    private void PushReleasedBlocks(Vector3 sawCenter, Vector3 pressDirection, float pressSpeed, float outwardForce,
        // Giải thích dòng gốc 972: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
        float tangentialForce, float spinDirection, float bladeRadius, float maxVelocity)
    // Giải thích dòng gốc 973: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 974: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (bladeRadius <= 0f || !EnsureEcsReady())
            // Giải thích dòng gốc 975: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return;

        // Giải thích dòng gốc 977: Nạp namespace NativeArray<Entity> entities = _releasedBlockQuery.ToEntityArray(Allocator.Temp) để file dùng được các kiểu và API trong đó.
        using NativeArray<Entity> entities = _releasedBlockQuery.ToEntityArray(Allocator.Temp);
        // Giải thích dòng gốc 978: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        using NativeArray<LocalTransform> transforms =
            // Giải thích dòng gốc 979: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _releasedBlockQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        // Giải thích dòng gốc 980: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        using NativeArray<ReleasedBlockComponent> blocks =
            // Giải thích dòng gốc 981: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _releasedBlockQuery.ToComponentDataArray<ReleasedBlockComponent>(Allocator.Temp);

        // Giải thích dòng gốc 983: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        float radiusSqr = bladeRadius * bladeRadius;
        // Giải thích dòng gốc 984: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int i = 0; i < entities.Length; i++)
        // Giải thích dòng gốc 985: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 986: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (blocks[i].OwnerId != _ownerId)
                // Giải thích dòng gốc 987: Bỏ qua phần còn lại của lần lặp hiện tại và sang lần tiếp theo.
                continue;

            // Giải thích dòng gốc 989: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            float3 entityPosition = transforms[i].Position;
            // Giải thích dòng gốc 990: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 delta = new Vector3(entityPosition.x - sawCenter.x, entityPosition.y - sawCenter.y, 0f);
            // Giải thích dòng gốc 991: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float distanceSqr = delta.sqrMagnitude;
            // Giải thích dòng gốc 992: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (distanceSqr > radiusSqr)
                // Giải thích dòng gốc 993: Bỏ qua phần còn lại của lần lặp hiện tại và sang lần tiếp theo.
                continue;

            // Giải thích dòng gốc 995: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float distance = Mathf.Sqrt(distanceSqr);
            // Giải thích dòng gốc 996: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 outward = distance > 0.0001f
                // Giải thích dòng gốc 997: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                ? delta / distance
                // Giải thích dòng gốc 998: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                : pressDirection.sqrMagnitude > 0.0001f ? pressDirection : Vector3.up;
            // Giải thích dòng gốc 999: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float radiusPush = 1f - Mathf.Clamp01(distance / bladeRadius);
            // Giải thích dòng gốc 1000: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 tangent = new Vector3(-outward.y, outward.x, 0f) * Mathf.Sign(spinDirection);
            // Giải thích dòng gốc 1001: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float speedScale = 1f + Mathf.Min(pressSpeed, 4f) * 0.1f;
            // Giải thích dòng gốc 1002: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 impulseVelocity =
                // Giải thích dòng gốc 1003: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                outward * (outwardForce * 0.08f * speedScale * radiusPush) +
                // Giải thích dòng gốc 1004: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                tangent * (tangentialForce * 0.22f * speedScale * radiusPush);

            // Giải thích dòng gốc 1006: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            PhysicsVelocity velocity = _entityManager.GetComponentData<PhysicsVelocity>(entities[i]);
            // Giải thích dòng gốc 1007: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 linear = new Vector3(velocity.Linear.x, velocity.Linear.y, 0f) + impulseVelocity;
            // Giải thích dòng gốc 1008: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            linear = Vector3.ClampMagnitude(linear, GetSafePhysicsVelocity(maxVelocity));
            // Giải thích dòng gốc 1009: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            linear = RedirectVelocityFromSolid(
                // Giải thích dòng gốc 1010: Tạo một instance mới của kiểu dữ liệu được chỉ định.
                new Vector3(entityPosition.x, entityPosition.y, entityPosition.z), linear);
            // Giải thích dòng gốc 1011: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            velocity.Linear = new float3(linear.x, linear.y, 0f);
            // Giải thích dòng gốc 1012: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _entityManager.SetComponentData(entities[i], velocity);
        // Giải thích dòng gốc 1013: Đóng khối code hiện tại.
        }
    // Giải thích dòng gốc 1014: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 1016: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Chuẩn hóa vận tốc tối đa hợp lệ trước khi ghi vào dữ liệu ECS.</summary>
    // Giải thích dòng gốc 1017: Khai báo hàm GetSafePhysicsVelocity với tham số trong ngoặc để thực hiện một hành vi.
    private float GetSafePhysicsVelocity(float requestedMaxVelocity)
    // Giải thích dòng gốc 1018: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 1019: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        float fixedDeltaTime = Mathf.Max(Time.fixedDeltaTime, 0.001f);
        // Giải thích dòng gốc 1020: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        float maxCellTravelVelocity = _cellSize * 0.75f / fixedDeltaTime;
        // Giải thích dòng gốc 1021: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
        return Mathf.Min(requestedMaxVelocity, maxCellTravelVelocity);
    // Giải thích dòng gốc 1022: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 1024: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Đổi hướng vận tốc khi debris sắp bị đẩy vào cell grid vẫn còn đặc.</summary>
    // Giải thích dòng gốc 1025: Khai báo hàm RedirectVelocityFromSolid với tham số trong ngoặc để thực hiện một hành vi.
    private Vector3 RedirectVelocityFromSolid(Vector3 worldPosition, Vector3 velocity)
    // Giải thích dòng gốc 1026: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 1027: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        velocity.z = 0f;
        // Giải thích dòng gốc 1028: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        float speed = velocity.magnitude;
        // Giải thích dòng gốc 1029: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (speed <= 0.0001f)
            // Giải thích dòng gốc 1030: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return velocity;

        // Giải thích dòng gốc 1032: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        Vector3 direction = velocity / speed;
        // Giải thích dòng gốc 1033: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        float nearProbeDistance = _cellSize * 0.9f;
        // Giải thích dòng gốc 1034: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        float farProbeDistance = _cellSize * 1.6f;
        // Giải thích dòng gốc 1035: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (!IsSolidAlongDirection(worldPosition, direction, nearProbeDistance, farProbeDistance))
            // Giải thích dòng gốc 1036: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return velocity;

        // Giải thích dòng gốc 1038: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        Vector3 tangent = new Vector3(-direction.y, direction.x, 0f);
        // Giải thích dòng gốc 1039: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        Vector3 bestDirection = Vector3.zero;
        // Giải thích dòng gốc 1040: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        float bestAlignment = float.MinValue;
        // Giải thích dòng gốc 1041: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        EvaluateFreeDirection(worldPosition, tangent, direction, nearProbeDistance, farProbeDistance,
            // Giải thích dòng gốc 1042: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
            ref bestDirection, ref bestAlignment);
        // Giải thích dòng gốc 1043: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        EvaluateFreeDirection(worldPosition, -tangent, direction, nearProbeDistance, farProbeDistance,
            // Giải thích dòng gốc 1044: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
            ref bestDirection, ref bestAlignment);
        // Giải thích dòng gốc 1045: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        EvaluateFreeDirection(worldPosition, Vector3.up, direction, nearProbeDistance, farProbeDistance,
            // Giải thích dòng gốc 1046: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
            ref bestDirection, ref bestAlignment);
        // Giải thích dòng gốc 1047: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        EvaluateFreeDirection(worldPosition, Vector3.down, direction, nearProbeDistance, farProbeDistance,
            // Giải thích dòng gốc 1048: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
            ref bestDirection, ref bestAlignment);
        // Giải thích dòng gốc 1049: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        EvaluateFreeDirection(worldPosition, Vector3.left, direction, nearProbeDistance, farProbeDistance,
            // Giải thích dòng gốc 1050: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
            ref bestDirection, ref bestAlignment);
        // Giải thích dòng gốc 1051: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        EvaluateFreeDirection(worldPosition, Vector3.right, direction, nearProbeDistance, farProbeDistance,
            // Giải thích dòng gốc 1052: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
            ref bestDirection, ref bestAlignment);

        // Giải thích dòng gốc 1054: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
        return bestDirection.sqrMagnitude > 0f ? bestDirection * speed : Vector3.zero;
    // Giải thích dòng gốc 1055: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 1057: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Đánh giá một hướng thay thế và giữ hướng thoát tốt nhất cho debris.</summary>
    // Giải thích dòng gốc 1058: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
    private void EvaluateFreeDirection(Vector3 worldPosition, Vector3 candidate, Vector3 desired,
        // Giải thích dòng gốc 1059: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
        float nearProbeDistance, float farProbeDistance, ref Vector3 bestDirection, ref float bestAlignment)
    // Giải thích dòng gốc 1060: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 1061: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        candidate.z = 0f;
        // Giải thích dòng gốc 1062: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        candidate.Normalize();
        // Giải thích dòng gốc 1063: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (IsSolidAlongDirection(worldPosition, candidate, nearProbeDistance, farProbeDistance))
            // Giải thích dòng gốc 1064: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return;

        // Giải thích dòng gốc 1066: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        float alignment = Vector3.Dot(candidate, desired);
        // Giải thích dòng gốc 1067: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (alignment <= bestAlignment)
            // Giải thích dòng gốc 1068: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return;

        // Giải thích dòng gốc 1070: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        bestAlignment = alignment;
        // Giải thích dòng gốc 1071: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        bestDirection = candidate;
    // Giải thích dòng gốc 1072: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 1074: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Kiểm tra đường ngắn phía trước có va vào cell đặc hay không.</summary>
    // Giải thích dòng gốc 1075: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
    private bool IsSolidAlongDirection(Vector3 worldPosition, Vector3 direction,
        // Giải thích dòng gốc 1076: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
        float nearProbeDistance, float farProbeDistance)
    // Giải thích dòng gốc 1077: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 1078: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
        return IsSolidAtWorldCell(worldPosition + direction * nearProbeDistance) ||
               // Giải thích dòng gốc 1079: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
               IsSolidAtWorldCell(worldPosition + direction * farProbeDistance);
    // Giải thích dòng gốc 1080: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 1082: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Kiểm tra đúng cell grid chứa vị trí world-space có còn đặc.</summary>
    // Giải thích dòng gốc 1083: Khai báo hàm IsSolidAtWorldCell với tham số trong ngoặc để thực hiện một hành vi.
    private bool IsSolidAtWorldCell(Vector3 worldPosition)
    // Giải thích dòng gốc 1084: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 1085: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (!_cellSolid.IsCreated || _runtimeParent == null)
            // Giải thích dòng gốc 1086: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return false;

        // Giải thích dòng gốc 1088: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        Vector3 local = _runtimeParent.InverseTransformPoint(worldPosition);
        // Giải thích dòng gốc 1089: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int x = Mathf.RoundToInt((local.x - _offset.x) / _cellSize);
        // Giải thích dòng gốc 1090: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int y = Mathf.RoundToInt((local.y - _offset.y) / _cellSize);
        // Giải thích dòng gốc 1091: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if ((uint)x >= (uint)_gridWidth || (uint)y >= (uint)_gridHeight)
            // Giải thích dòng gốc 1092: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return false;

        // Giải thích dòng gốc 1094: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
        return _cellSolid[y * _gridWidth + x] != 0;
    // Giải thích dòng gốc 1095: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 1097: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Lưu lực cưa để áp dụng một lần trong LateUpdate, tránh update ECS nhiều lần mỗi frame.</summary>
    // Giải thích dòng gốc 1098: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
    private void QueueSawPush(Vector3 sawCenter, Vector3 pressDirection, float pressSpeed, float outwardForce,
        // Giải thích dòng gốc 1099: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
        float tangentialForce, float spinDirection, float bladeRadius, float maxVelocity)
    // Giải thích dòng gốc 1100: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 1101: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _hasPendingSawPush = true;
        // Giải thích dòng gốc 1102: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _pendingSawCenter = sawCenter;
        // Giải thích dòng gốc 1103: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _pendingSawDirection = pressDirection;
        // Giải thích dòng gốc 1104: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _pendingSawSpeed = pressSpeed;
        // Giải thích dòng gốc 1105: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _pendingSawOutwardForce = outwardForce;
        // Giải thích dòng gốc 1106: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _pendingSawTangentialForce = tangentialForce;
        // Giải thích dòng gốc 1107: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _pendingSawSpinDirection = spinDirection;
        // Giải thích dòng gốc 1108: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _pendingSawRadius = bladeRadius;
        // Giải thích dòng gốc 1109: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _pendingSawMaxVelocity = maxVelocity;
    // Giải thích dòng gốc 1110: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 1112: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Áp dụng lệnh lực cưa đã queue rồi xóa trạng thái pending.</summary>
    // Giải thích dòng gốc 1113: Khai báo hàm ApplyPendingSawPush với tham số trong ngoặc để thực hiện một hành vi.
    private void ApplyPendingSawPush()
    // Giải thích dòng gốc 1114: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 1115: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (!_hasPendingSawPush)
            // Giải thích dòng gốc 1116: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return;

        // Giải thích dòng gốc 1118: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _hasPendingSawPush = false;
        // Giải thích dòng gốc 1119: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        PushReleasedBlocks(_pendingSawCenter, _pendingSawDirection, _pendingSawSpeed, _pendingSawOutwardForce,
            // Giải thích dòng gốc 1120: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
            _pendingSawTangentialForce, _pendingSawSpinDirection, _pendingSawRadius, _pendingSawMaxVelocity);
    // Giải thích dòng gốc 1121: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 1123: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Chuẩn bị và vẽ debris ECS bằng Graphics.DrawMeshInstanced.</summary>
    // Giải thích dòng gốc 1124: Khai báo hàm DrawReleasedBlocks với tham số trong ngoặc để thực hiện một hành vi.
    private void DrawReleasedBlocks()
    // Giải thích dòng gốc 1125: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 1126: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (!EnsureEcsReady() || !EnsureReleasedBlockResources())
            // Giải thích dòng gốc 1127: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return;

        // Giải thích dòng gốc 1129: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        ConsumePreparedRenderFrame();
        // Giải thích dòng gốc 1130: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        DrawCachedRenderFrame();

        // Giải thích dòng gốc 1132: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_displayRenderFrame < 0 || Time.frameCount % _releasedBlockRenderInterval == 0)
            // Giải thích dòng gốc 1133: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            ScheduleRenderPreparation();
    // Giải thích dòng gốc 1134: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 1136: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Đọc kết quả job render đã xong và đổi frame hiển thị.</summary>
    // Giải thích dòng gốc 1137: Khai báo hàm ConsumePreparedRenderFrame với tham số trong ngoặc để thực hiện một hành vi.
    private void ConsumePreparedRenderFrame()
    // Giải thích dòng gốc 1138: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 1139: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int i = 0; i < _renderFrames.Length; i++)
        // Giải thích dòng gốc 1140: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 1141: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            RenderFrameData frame = _renderFrames[i];
            // Giải thích dòng gốc 1142: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (frame == null || !frame.Pending || !frame.Handle.IsCompleted)
                // Giải thích dòng gốc 1143: Bỏ qua phần còn lại của lần lặp hiện tại và sang lần tiếp theo.
                continue;

            // Giải thích dòng gốc 1145: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            frame.Handle.Complete();
            // Giải thích dòng gốc 1146: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            frame.Pending = false;
            // Giải thích dòng gốc 1147: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _displayRenderFrame = i;
            // Giải thích dòng gốc 1148: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            CachePreparedRenderFrame(frame);
        // Giải thích dòng gốc 1149: Đóng khối code hiện tại.
        }
    // Giải thích dòng gốc 1150: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 1152: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Schedule job copy transform/màu debris ra buffer render không cấp phát GC.</summary>
    // Giải thích dòng gốc 1153: Khai báo hàm ScheduleRenderPreparation với tham số trong ngoặc để thực hiện một hành vi.
    private void ScheduleRenderPreparation()
    // Giải thích dòng gốc 1154: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 1155: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int writeIndex = _displayRenderFrame == 0 ? 1 : 0;
        // Giải thích dòng gốc 1156: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        RenderFrameData frame = _renderFrames[writeIndex];
        // Giải thích dòng gốc 1157: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (frame == null || frame.Pending)
            // Giải thích dòng gốc 1158: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return;

        // Giải thích dòng gốc 1160: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        NativeArray<LocalTransform> transforms =
            // Giải thích dòng gốc 1161: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _releasedBlockQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        // Giải thích dòng gốc 1162: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        NativeArray<ReleasedBlockComponent> blocks =
            // Giải thích dòng gốc 1163: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _releasedBlockQuery.ToComponentDataArray<ReleasedBlockComponent>(Allocator.TempJob);

        // Giải thích dòng gốc 1165: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        frame.Count.Value = 0;
        // Giải thích dòng gốc 1166: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        JobHandle prepareHandle = new PrepareRenderFrameJob
        // Giải thích dòng gốc 1167: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 1168: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Transforms = transforms,
            // Giải thích dòng gốc 1169: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Blocks = blocks,
            // Giải thích dòng gốc 1170: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Matrices = frame.Matrices,
            // Giải thích dòng gốc 1171: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Colors = frame.Colors,
            // Giải thích dòng gốc 1172: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Count = frame.Count,
            // Giải thích dòng gốc 1173: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            OwnerId = _ownerId
        // Giải thích dòng gốc 1174: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
        }.Schedule();

        // Giải thích dòng gốc 1176: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        JobHandle disposeTransforms = transforms.Dispose(prepareHandle);
        // Giải thích dòng gốc 1177: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        JobHandle disposeBlocks = blocks.Dispose(prepareHandle);
        // Giải thích dòng gốc 1178: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        frame.Handle = JobHandle.CombineDependencies(disposeTransforms, disposeBlocks);
        // Giải thích dòng gốc 1179: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        frame.Pending = true;
    // Giải thích dòng gốc 1180: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 1182: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Chuyển matrix/màu native sang batch managed tối đa 1023 instance để vẽ Unity.</summary>
    // Giải thích dòng gốc 1183: Khai báo hàm CachePreparedRenderFrame với tham số trong ngoặc để thực hiện một hành vi.
    private void CachePreparedRenderFrame(RenderFrameData frame)
    // Giải thích dòng gốc 1184: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 1185: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int count = frame.Count.Value;
        // Giải thích dòng gốc 1186: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int sourceIndex = 0;
        // Giải thích dòng gốc 1187: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int batchIndex = 0;
        // Giải thích dòng gốc 1188: Bắt đầu vòng lặp while và tiếp tục lặp khi điều kiện còn đúng.
        while (sourceIndex < count)
        // Giải thích dòng gốc 1189: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 1190: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Matrix4x4[] matrices = _renderBatchMatrices[batchIndex];
            // Giải thích dòng gốc 1191: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector4[] colors = _renderBatchColors[batchIndex];
            // Giải thích dòng gốc 1192: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            int batchCount = Mathf.Min(matrices.Length, count - sourceIndex);
            // Giải thích dòng gốc 1193: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
            for (int i = 0; i < batchCount; i++)
            // Giải thích dòng gốc 1194: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
            {
                // Giải thích dòng gốc 1195: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                float4x4 matrix = frame.Matrices[sourceIndex + i];
                // Giải thích dòng gốc 1196: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                matrices[i] = new Matrix4x4(matrix.c0, matrix.c1, matrix.c2, matrix.c3);
                // Giải thích dòng gốc 1197: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                colors[i] = frame.Colors[sourceIndex + i];
            // Giải thích dòng gốc 1198: Đóng khối code hiện tại.
            }

            // Giải thích dòng gốc 1200: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _renderBatchCounts[batchIndex] = batchCount;
            // Giải thích dòng gốc 1201: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            sourceIndex += batchCount;
            // Giải thích dòng gốc 1202: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
            batchIndex++;
        // Giải thích dòng gốc 1203: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 1205: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _renderBatchCount = batchIndex;
    // Giải thích dòng gốc 1206: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 1208: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Gửi các batch matrix/màu đã cache tới GPU.</summary>
    // Giải thích dòng gốc 1209: Khai báo hàm DrawCachedRenderFrame với tham số trong ngoặc để thực hiện một hành vi.
    private void DrawCachedRenderFrame()
    // Giải thích dòng gốc 1210: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 1211: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int i = 0; i < _renderBatchCount; i++)
        // Giải thích dòng gốc 1212: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 1213: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector4[] colors = _renderBatchColors[i];
            // Giải thích dòng gốc 1214: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _releasedBlockPropertyBlock.Clear();
            // Giải thích dòng gốc 1215: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _releasedBlockPropertyBlock.SetVectorArray(ColorId, colors);
            // Giải thích dòng gốc 1216: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            Graphics.DrawMeshInstanced(_releasedBlockMesh, 0, _releasedBlockMaterial, _renderBatchMatrices[i],
                // Giải thích dòng gốc 1217: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                _renderBatchCounts[i], _releasedBlockPropertyBlock, ShadowCastingMode.Off, false, gameObject.layer);
        // Giải thích dòng gốc 1218: Đóng khối code hiện tại.
        }
    // Giải thích dòng gốc 1219: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 1221: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Loại entity debris đã bị destroy khỏi danh sách tracking local.</summary>
    // Giải thích dòng gốc 1222: Khai báo hàm TrimReleasedBlockEntityList với tham số trong ngoặc để thực hiện một hành vi.
    private void TrimReleasedBlockEntityList()
    // Giải thích dòng gốc 1223: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 1224: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (!EnsureEcsReady())
            // Giải thích dòng gốc 1225: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return;

        // Giải thích dòng gốc 1227: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int i = _releasedBlockEntities.Count - 1; i >= 0; i--)
            // Giải thích dòng gốc 1228: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (!_entityManager.Exists(_releasedBlockEntities[i]))
                // Giải thích dòng gốc 1229: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                _releasedBlockEntities.RemoveAt(i);
    // Giải thích dòng gốc 1230: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 1232: Comment XML documentation mô tả API cho IDE và tài liệu.
    /// <summary>Destroy toàn bộ debris entity thuộc spawner hiện tại.</summary>
    // Giải thích dòng gốc 1233: Khai báo hàm ClearReleasedBlockEntities với tham số trong ngoặc để thực hiện một hành vi.
    private void ClearReleasedBlockEntities()
    // Giải thích dòng gốc 1234: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 1235: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (!EnsureEcsReady())
        // Giải thích dòng gốc 1236: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 1237: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _releasedBlockEntities.Clear();
            // Giải thích dòng gốc 1238: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return;
        // Giải thích dòng gốc 1239: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 1241: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int i = _releasedBlockEntities.Count - 1; i >= 0; i--)
            // Giải thích dòng gốc 1242: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            DestroyReleasedBlockEntityAt(i);

        // Giải thích dòng gốc 1244: Nạp namespace NativeArray<Entity> entities = _releasedBlockQuery.ToEntityArray(Allocator.Temp) để file dùng được các kiểu và API trong đó.
        using NativeArray<Entity> entities = _releasedBlockQuery.ToEntityArray(Allocator.Temp);
        // Giải thích dòng gốc 1245: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        using NativeArray<ReleasedBlockComponent> blocks =
            // Giải thích dòng gốc 1246: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _releasedBlockQuery.ToComponentDataArray<ReleasedBlockComponent>(Allocator.Temp);
        // Giải thích dòng gốc 1247: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int i = 0; i < entities.Length; i++)
            // Giải thích dòng gốc 1248: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (blocks[i].OwnerId == _ownerId && _entityManager.Exists(entities[i]))
                // Giải thích dòng gốc 1249: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                _entityManager.DestroyEntity(entities[i]);
    // Giải thích dòng gốc 1250: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 1252: Khai báo hàm DespawnInBounds với tham số trong ngoặc để thực hiện một hành vi.
    private void DespawnInBounds(Bounds bounds)
    // Giải thích dòng gốc 1253: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 1254: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (!EnsureEcsReady())
            // Giải thích dòng gốc 1255: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return;

        // Giải thích dòng gốc 1257: Nạp namespace NativeArray<Entity> entities = _releasedBlockQuery.ToEntityArray(Allocator.Temp) để file dùng được các kiểu và API trong đó.
        using NativeArray<Entity> entities = _releasedBlockQuery.ToEntityArray(Allocator.Temp);
        // Giải thích dòng gốc 1258: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        using NativeArray<LocalTransform> transforms =
            // Giải thích dòng gốc 1259: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _releasedBlockQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        // Giải thích dòng gốc 1260: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        using NativeArray<ReleasedBlockComponent> blocks =
            // Giải thích dòng gốc 1261: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _releasedBlockQuery.ToComponentDataArray<ReleasedBlockComponent>(Allocator.Temp);

        // Giải thích dòng gốc 1263: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int i = 0; i < entities.Length; i++)
        // Giải thích dòng gốc 1264: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 1265: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (blocks[i].OwnerId != _ownerId)
                // Giải thích dòng gốc 1266: Bỏ qua phần còn lại của lần lặp hiện tại và sang lần tiếp theo.
                continue;

            // Giải thích dòng gốc 1268: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            float3 position = transforms[i].Position;
            // Giải thích dòng gốc 1269: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (bounds.Contains(new Vector3(position.x, position.y, position.z)) && _entityManager.Exists(entities[i]))
                // Giải thích dòng gốc 1270: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                _entityManager.DestroyEntity(entities[i]);
        // Giải thích dòng gốc 1271: Đóng khối code hiện tại.
        }
    // Giải thích dòng gốc 1272: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 1274: Khai báo hàm ApplyConveyor với tham số trong ngoặc để thực hiện một hành vi.
    private void ApplyConveyor(Bounds bounds, Vector3 direction, float speed, float acceleration, float deltaTime)
    // Giải thích dòng gốc 1275: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 1276: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (!EnsureEcsReady())
            // Giải thích dòng gốc 1277: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return;

        // Giải thích dòng gốc 1279: Nạp namespace NativeArray<Entity> entities = _releasedBlockQuery.ToEntityArray(Allocator.Temp) để file dùng được các kiểu và API trong đó.
        using NativeArray<Entity> entities = _releasedBlockQuery.ToEntityArray(Allocator.Temp);
        // Giải thích dòng gốc 1280: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        using NativeArray<LocalTransform> transforms =
            // Giải thích dòng gốc 1281: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _releasedBlockQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        // Giải thích dòng gốc 1282: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        using NativeArray<ReleasedBlockComponent> blocks =
            // Giải thích dòng gốc 1283: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _releasedBlockQuery.ToComponentDataArray<ReleasedBlockComponent>(Allocator.Temp);

        // Giải thích dòng gốc 1285: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        float3 conveyorDirection = new float3(direction.x, direction.y, direction.z);
        // Giải thích dòng gốc 1286: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int i = 0; i < entities.Length; i++)
        // Giải thích dòng gốc 1287: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 1288: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (blocks[i].OwnerId != _ownerId)
                // Giải thích dòng gốc 1289: Bỏ qua phần còn lại của lần lặp hiện tại và sang lần tiếp theo.
                continue;

            // Giải thích dòng gốc 1291: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            float3 position = transforms[i].Position;
            // Giải thích dòng gốc 1292: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (!bounds.Contains(new Vector3(position.x, position.y, position.z)))
                // Giải thích dòng gốc 1293: Bỏ qua phần còn lại của lần lặp hiện tại và sang lần tiếp theo.
                continue;

            // Giải thích dòng gốc 1295: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            PhysicsVelocity velocity = _entityManager.GetComponentData<PhysicsVelocity>(entities[i]);
            // Giải thích dòng gốc 1296: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float currentSpeed = math.dot(velocity.Linear, conveyorDirection);
            // Giải thích dòng gốc 1297: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            velocity.Linear += conveyorDirection *
                               // Giải thích dòng gốc 1298: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                               (Mathf.MoveTowards(currentSpeed, speed, acceleration * deltaTime) - currentSpeed);
            // Giải thích dòng gốc 1299: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _entityManager.SetComponentData(entities[i], velocity);
        // Giải thích dòng gốc 1300: Đóng khối code hiện tại.
        }
    // Giải thích dòng gốc 1301: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 1303: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
    private void ApplySuction(Vector3 origin, Quaternion rotation, Vector3 boxSize, float force, float acceleration,
        // Giải thích dòng gốc 1304: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
        float maxVelocity, float arrivalDamping, float destroyRadius, float deltaTime)
    // Giải thích dòng gốc 1305: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 1306: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (!EnsureEcsReady())
            // Giải thích dòng gốc 1307: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return;

        // Giải thích dòng gốc 1309: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        Quaternion inverseRotation = Quaternion.Inverse(rotation);
        // Giải thích dòng gốc 1310: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        Vector3 halfSize = boxSize * 0.5f;
        // Giải thích dòng gốc 1311: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        Vector3 boxOffset = rotation * new Vector3(boxSize.x * 0.5f, 0f, 0f);

        // Giải thích dòng gốc 1313: Nạp namespace NativeArray<Entity> entities = _releasedBlockQuery.ToEntityArray(Allocator.Temp) để file dùng được các kiểu và API trong đó.
        using NativeArray<Entity> entities = _releasedBlockQuery.ToEntityArray(Allocator.Temp);
        // Giải thích dòng gốc 1314: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        using NativeArray<LocalTransform> transforms =
            // Giải thích dòng gốc 1315: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _releasedBlockQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        // Giải thích dòng gốc 1316: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        using NativeArray<ReleasedBlockComponent> blocks =
            // Giải thích dòng gốc 1317: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _releasedBlockQuery.ToComponentDataArray<ReleasedBlockComponent>(Allocator.Temp);

        // Giải thích dòng gốc 1319: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int i = 0; i < entities.Length; i++)
        // Giải thích dòng gốc 1320: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 1321: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (blocks[i].OwnerId != _ownerId)
                // Giải thích dòng gốc 1322: Bỏ qua phần còn lại của lần lặp hiện tại và sang lần tiếp theo.
                continue;

            // Giải thích dòng gốc 1324: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            float3 transformPosition = transforms[i].Position;
            // Giải thích dòng gốc 1325: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 position = new Vector3(transformPosition.x, transformPosition.y, transformPosition.z);
            // Giải thích dòng gốc 1326: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 local = inverseRotation * (position - origin - boxOffset);
            // Giải thích dòng gốc 1327: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (Mathf.Abs(local.x) > halfSize.x || Mathf.Abs(local.y) > halfSize.y || Mathf.Abs(local.z) > halfSize.z)
                // Giải thích dòng gốc 1328: Bỏ qua phần còn lại của lần lặp hiện tại và sang lần tiếp theo.
                continue;

            // Giải thích dòng gốc 1330: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 direction = origin - position;
            // Giải thích dòng gốc 1331: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            direction.z = 0f;
            // Giải thích dòng gốc 1332: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float distance = direction.magnitude;
            // Giải thích dòng gốc 1333: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (distance < destroyRadius)
            // Giải thích dòng gốc 1334: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
            {
                // Giải thích dòng gốc 1335: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
                if (_entityManager.Exists(entities[i]))
                    // Giải thích dòng gốc 1336: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                    _entityManager.DestroyEntity(entities[i]);
                // Giải thích dòng gốc 1337: Bỏ qua phần còn lại của lần lặp hiện tại và sang lần tiếp theo.
                continue;
            // Giải thích dòng gốc 1338: Đóng khối code hiện tại.
            }

            // Giải thích dòng gốc 1340: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            direction /= distance;
            // Giải thích dòng gốc 1341: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            PhysicsVelocity velocity = _entityManager.GetComponentData<PhysicsVelocity>(entities[i]);
            // Giải thích dòng gốc 1342: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 currentVelocity = new Vector3(velocity.Linear.x, velocity.Linear.y, velocity.Linear.z);
            // Giải thích dòng gốc 1343: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 targetVelocity = direction * Mathf.Min(force * distance, maxVelocity);
            // Giải thích dòng gốc 1344: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 movedVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, acceleration * deltaTime);
            // Giải thích dòng gốc 1345: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (distance <= destroyRadius * 2.5f)
                // Giải thích dòng gốc 1346: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                movedVelocity = Vector3.Lerp(movedVelocity, targetVelocity, arrivalDamping * deltaTime);
            // Giải thích dòng gốc 1347: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            movedVelocity = Vector3.ClampMagnitude(movedVelocity, maxVelocity);
            // Giải thích dòng gốc 1348: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            velocity.Linear = new float3(movedVelocity.x, movedVelocity.y, movedVelocity.z);
            // Giải thích dòng gốc 1349: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _entityManager.SetComponentData(entities[i], velocity);
        // Giải thích dòng gốc 1350: Đóng khối code hiện tại.
        }
    // Giải thích dòng gốc 1351: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 1353: Khai báo hàm DestroyReleasedBlockEntityAt với tham số trong ngoặc để thực hiện một hành vi.
    private void DestroyReleasedBlockEntityAt(int index)
    // Giải thích dòng gốc 1354: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 1355: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (!EnsureEcsReady())
            // Giải thích dòng gốc 1356: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return;

        // Giải thích dòng gốc 1358: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        Entity entity = _releasedBlockEntities[index];
        // Giải thích dòng gốc 1359: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        _releasedBlockEntities.RemoveAt(index);
        // Giải thích dòng gốc 1360: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_entityManager.Exists(entity))
            // Giải thích dòng gốc 1361: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _entityManager.DestroyEntity(entity);
    // Giải thích dòng gốc 1362: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 1364: Khai báo hàm DisposeReleasedBlocks với tham số trong ngoặc để thực hiện một hành vi.
    private void DisposeReleasedBlocks()
    // Giải thích dòng gốc 1365: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 1366: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        ClearReleasedBlockEntities();
        // Giải thích dòng gốc 1367: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        _releasedBlockEntities.Clear();
    // Giải thích dòng gốc 1368: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 1370: Khai báo hàm DisposeReleasedBlockResources với tham số trong ngoặc để thực hiện một hành vi.
    private void DisposeReleasedBlockResources()
    // Giải thích dòng gốc 1371: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 1372: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        DisposeRenderFrameResources();

        // Giải thích dòng gốc 1374: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_releasedBlockCollider.IsCreated)
        // Giải thích dòng gốc 1375: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 1376: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _releasedBlockCollider.Dispose();
            // Giải thích dòng gốc 1377: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _releasedBlockCollider = default;
        // Giải thích dòng gốc 1378: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 1380: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_releasedBlockMaterial != null)
            // Giải thích dòng gốc 1381: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            DestroyUnityObject(_releasedBlockMaterial);
        // Giải thích dòng gốc 1382: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _releasedBlockMaterial = null;
        // Giải thích dòng gốc 1383: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _releasedBlockMesh = null;
    // Giải thích dòng gốc 1384: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 1386: Khai báo hàm EnsureRenderFrameResources với tham số trong ngoặc để thực hiện một hành vi.
    private void EnsureRenderFrameResources()
    // Giải thích dòng gốc 1387: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 1388: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_renderBatchMatrices == null)
        // Giải thích dòng gốc 1389: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 1390: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            int batchCapacity = Mathf.CeilToInt(_maxReleasedPhysicsBlocks / 1023f);
            // Giải thích dòng gốc 1391: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _renderBatchMatrices = new Matrix4x4[batchCapacity][];
            // Giải thích dòng gốc 1392: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _renderBatchColors = new Vector4[batchCapacity][];
            // Giải thích dòng gốc 1393: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _renderBatchCounts = new int[batchCapacity];
            // Giải thích dòng gốc 1394: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
            for (int i = 0; i < batchCapacity; i++)
            // Giải thích dòng gốc 1395: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
            {
                // Giải thích dòng gốc 1396: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                _renderBatchMatrices[i] = new Matrix4x4[1023];
                // Giải thích dòng gốc 1397: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                _renderBatchColors[i] = new Vector4[1023];
            // Giải thích dòng gốc 1398: Đóng khối code hiện tại.
            }
        // Giải thích dòng gốc 1399: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 1401: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int i = 0; i < _renderFrames.Length; i++)
        // Giải thích dòng gốc 1402: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 1403: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (_renderFrames[i] != null)
                // Giải thích dòng gốc 1404: Bỏ qua phần còn lại của lần lặp hiện tại và sang lần tiếp theo.
                continue;

            // Giải thích dòng gốc 1406: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _renderFrames[i] = new RenderFrameData(_maxReleasedPhysicsBlocks);
        // Giải thích dòng gốc 1407: Đóng khối code hiện tại.
        }
    // Giải thích dòng gốc 1408: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 1410: Khai báo hàm DisposeRenderFrameResources với tham số trong ngoặc để thực hiện một hành vi.
    private void DisposeRenderFrameResources()
    // Giải thích dòng gốc 1411: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 1412: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int i = 0; i < _renderFrames.Length; i++)
        // Giải thích dòng gốc 1413: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 1414: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            RenderFrameData frame = _renderFrames[i];
            // Giải thích dòng gốc 1415: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (frame == null)
                // Giải thích dòng gốc 1416: Bỏ qua phần còn lại của lần lặp hiện tại và sang lần tiếp theo.
                continue;

            // Giải thích dòng gốc 1418: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (frame.Pending)
                // Giải thích dòng gốc 1419: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                frame.Handle.Complete();
            // Giải thích dòng gốc 1420: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            frame.Dispose();
            // Giải thích dòng gốc 1421: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _renderFrames[i] = null;
        // Giải thích dòng gốc 1422: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 1424: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _displayRenderFrame = -1;
        // Giải thích dòng gốc 1425: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _renderBatchCount = 0;
        // Giải thích dòng gốc 1426: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _renderBatchMatrices = null;
        // Giải thích dòng gốc 1427: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _renderBatchColors = null;
        // Giải thích dòng gốc 1428: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _renderBatchCounts = null;
    // Giải thích dòng gốc 1429: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 1431: Khai báo hàm DisposeReleasedBlockWalls với tham số trong ngoặc để thực hiện một hành vi.
    private void DisposeReleasedBlockWalls()
    // Giải thích dòng gốc 1432: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 1433: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_ecsWorld != null && _ecsWorld.IsCreated)
        // Giải thích dòng gốc 1434: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 1435: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
            for (int i = 0; i < _releasedBlockWallEntities.Count; i++)
            // Giải thích dòng gốc 1436: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
            {
                // Giải thích dòng gốc 1437: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                Entity entity = _releasedBlockWallEntities[i];
                // Giải thích dòng gốc 1438: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
                if (_entityManager.Exists(entity))
                    // Giải thích dòng gốc 1439: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                    _entityManager.DestroyEntity(entity);
            // Giải thích dòng gốc 1440: Đóng khối code hiện tại.
            }
        // Giải thích dòng gốc 1441: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 1443: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        _releasedBlockWallEntities.Clear();
        // Giải thích dòng gốc 1444: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int i = 0; i < _releasedBlockWallColliders.Count; i++)
        // Giải thích dòng gốc 1445: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 1446: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (_releasedBlockWallColliders[i].IsCreated)
                // Giải thích dòng gốc 1447: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                _releasedBlockWallColliders[i].Dispose();
        // Giải thích dòng gốc 1448: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 1450: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        _releasedBlockWallColliders.Clear();
    // Giải thích dòng gốc 1451: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 1453: Khai báo hàm DisposeEcsQuery với tham số trong ngoặc để thực hiện một hành vi.
    private void DisposeEcsQuery()
    // Giải thích dòng gốc 1454: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 1455: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_hasReleasedBlockQuery && _ecsWorld != null && _ecsWorld.IsCreated)
        // Giải thích dòng gốc 1456: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 1457: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _releasedBlockQuery.Dispose();
        // Giải thích dòng gốc 1458: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 1460: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _releasedBlockQuery = default;
        // Giải thích dòng gốc 1461: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _hasReleasedBlockQuery = false;
    // Giải thích dòng gốc 1462: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 1464: Khai báo hàm CreatePhysicsMaterial với tham số trong ngoặc để thực hiện một hành vi.
    private PhysicsMaterial CreatePhysicsMaterial()
    // Giải thích dòng gốc 1465: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 1466: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        PhysicsMaterial material = PhysicsMaterial.Default;
        // Giải thích dòng gốc 1467: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        material.Friction = _physicsFriction;
        // Giải thích dòng gốc 1468: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        material.Restitution = _physicsRestitution;
        // Giải thích dòng gốc 1469: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
        return material;
    // Giải thích dòng gốc 1470: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 1472: Khai báo hàm ToFloat4 với tham số trong ngoặc để thực hiện một hành vi.
    private static Vector4 ToFloat4(Color32 color)
    // Giải thích dòng gốc 1473: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 1474: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
        return new Vector4(color.r / 255f, color.g / 255f, color.b / 255f, color.a / 255f);
    // Giải thích dòng gốc 1475: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 1477: Khai báo hàm DestroyUnityObject với tham số trong ngoặc để thực hiện một hành vi.
    private static void DestroyUnityObject(Object target)
    // Giải thích dòng gốc 1478: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 1479: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
    // Giải thích dòng gốc 1480: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 1482: Khai báo hàm GetVoxelRotationY với tham số trong ngoặc để thực hiện một hành vi.
    private static float GetVoxelRotationY(int x, int y, float maxAngle)
    // Giải thích dòng gốc 1483: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 1484: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (maxAngle <= 0f)
            // Giải thích dòng gốc 1485: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return 0f;

        // Giải thích dòng gốc 1487: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        uint hash = (uint)(x * 73856093) ^ (uint)(y * 19349663);
        // Giải thích dòng gốc 1488: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        float normalized = (hash & 1023u) * (1f / 1023f);
        // Giải thích dòng gốc 1489: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
        return (normalized * 2f - 1f) * maxAngle;
    // Giải thích dòng gốc 1490: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 1492: Khai báo hàm DisposeCells với tham số trong ngoặc để thực hiện một hành vi.
    private void DisposeCells()
    // Giải thích dòng gốc 1493: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 1494: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_cellColors.IsCreated)
            // Giải thích dòng gốc 1495: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _cellColors.Dispose();

        // Giải thích dòng gốc 1497: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_cellSolid.IsCreated)
            // Giải thích dòng gốc 1498: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            _cellSolid.Dispose();
    // Giải thích dòng gốc 1499: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 1501: Khai báo struct TextureBlockMeshBuilder là kiểu dữ liệu value type.
    internal struct TextureBlockMeshBuilder
    // Giải thích dòng gốc 1502: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 1503: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
        [ReadOnly] public NativeArray<Color32> CellColors;
        // Giải thích dòng gốc 1504: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
        [ReadOnly] public NativeArray<byte> CellSolid;
        // Giải thích dòng gốc 1505: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        public NativeArray<byte> Visited;
        // Giải thích dòng gốc 1506: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        public NativeList<Vector3> Vertices;
        // Giải thích dòng gốc 1507: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        public NativeList<Color32> Colors;
        // Giải thích dòng gốc 1508: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        public NativeList<Vector2> Uvs;
        // Giải thích dòng gốc 1509: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        public NativeList<int> Indices;
        // Giải thích dòng gốc 1510: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        public int GridWidth;
        // Giải thích dòng gốc 1511: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        public int StartX;
        // Giải thích dòng gốc 1512: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        public int StartY;
        // Giải thích dòng gốc 1513: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        public int ChunkWidth;
        // Giải thích dòng gốc 1514: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        public int ChunkHeight;
        // Giải thích dòng gốc 1515: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        public float CellSize;
        // Giải thích dòng gốc 1516: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        public Vector3 Offset;
        // Giải thích dòng gốc 1517: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        public byte UseVoxelDetail;
        // Giải thích dòng gốc 1518: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        public byte ExtrudeMergedQuads;
        // Giải thích dòng gốc 1519: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        public byte MergeAnySolid;
        // Giải thích dòng gốc 1520: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        public float DetailVoxelScale;
        // Giải thích dòng gốc 1521: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        public float RandomYRotation;
        // Giải thích dòng gốc 1522: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        public float DetailDepth;

        // Giải thích dòng gốc 1524: Khai báo hàm Execute với tham số trong ngoặc để thực hiện một hành vi.
        public void Execute()
        // Giải thích dòng gốc 1525: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 1526: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
            for (int i = 0; i < Visited.Length; i++)
                // Giải thích dòng gốc 1527: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                Visited[i] = 0;

            // Giải thích dòng gốc 1529: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
            for (int y = 0; y < ChunkHeight; y++)
            // Giải thích dòng gốc 1530: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
            {
                // Giải thích dòng gốc 1531: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
                for (int x = 0; x < ChunkWidth; x++)
                // Giải thích dòng gốc 1532: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
                {
                    // Giải thích dòng gốc 1533: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                    int localIndex = y * ChunkWidth + x;
                    // Giải thích dòng gốc 1534: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
                    if (Visited[localIndex] != 0)
                        // Giải thích dòng gốc 1535: Bỏ qua phần còn lại của lần lặp hiện tại và sang lần tiếp theo.
                        continue;

                    // Giải thích dòng gốc 1537: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                    int cellIndex = (StartY + y) * GridWidth + StartX + x;
                    // Giải thích dòng gốc 1538: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
                    if (CellSolid[cellIndex] == 0)
                        // Giải thích dòng gốc 1539: Bỏ qua phần còn lại của lần lặp hiện tại và sang lần tiếp theo.
                        continue;

                    // Giải thích dòng gốc 1541: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                    Color32 color = CellColors[cellIndex];
                    // Giải thích dòng gốc 1542: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
                    if (UseVoxelDetail != 0)
                    // Giải thích dòng gốc 1543: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
                    {
                        // Giải thích dòng gốc 1544: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                        Visited[localIndex] = 1;
                        // Giải thích dòng gốc 1545: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                        AddVoxelBox(x, y, color);
                        // Giải thích dòng gốc 1546: Bỏ qua phần còn lại của lần lặp hiện tại và sang lần tiếp theo.
                        continue;
                    // Giải thích dòng gốc 1547: Đóng khối code hiện tại.
                    }

                    // Giải thích dòng gốc 1549: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                    int rectWidth = 1;
                    // Giải thích dòng gốc 1550: Bắt đầu vòng lặp while và tiếp tục lặp khi điều kiện còn đúng.
                    while (x + rectWidth < ChunkWidth && CanMerge(x + rectWidth, y, color))
                        // Giải thích dòng gốc 1551: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                        rectWidth++;

                    // Giải thích dòng gốc 1553: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                    int rectHeight = 1;
                    // Giải thích dòng gốc 1554: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                    bool canGrow = true;
                    // Giải thích dòng gốc 1555: Bắt đầu vòng lặp while và tiếp tục lặp khi điều kiện còn đúng.
                    while (y + rectHeight < ChunkHeight && canGrow)
                    // Giải thích dòng gốc 1556: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
                    {
                        // Giải thích dòng gốc 1557: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
                        for (int scanX = 0; scanX < rectWidth; scanX++)
                        // Giải thích dòng gốc 1558: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
                        {
                            // Giải thích dòng gốc 1559: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
                            if (!CanMerge(x + scanX, y + rectHeight, color))
                            // Giải thích dòng gốc 1560: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
                            {
                                // Giải thích dòng gốc 1561: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                                canGrow = false;
                                // Giải thích dòng gốc 1562: Thoát khỏi vòng lặp hoặc switch hiện tại.
                                break;
                            // Giải thích dòng gốc 1563: Đóng khối code hiện tại.
                            }
                        // Giải thích dòng gốc 1564: Đóng khối code hiện tại.
                        }

                        // Giải thích dòng gốc 1566: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
                        if (canGrow)
                            // Giải thích dòng gốc 1567: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                            rectHeight++;
                    // Giải thích dòng gốc 1568: Đóng khối code hiện tại.
                    }

                    // Giải thích dòng gốc 1570: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
                    for (int fillY = 0; fillY < rectHeight; fillY++)
                    // Giải thích dòng gốc 1571: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
                    {
                        // Giải thích dòng gốc 1572: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
                        for (int fillX = 0; fillX < rectWidth; fillX++)
                            // Giải thích dòng gốc 1573: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                            Visited[(y + fillY) * ChunkWidth + x + fillX] = 1;
                    // Giải thích dòng gốc 1574: Đóng khối code hiện tại.
                    }

                    // Giải thích dòng gốc 1576: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
                    if (ExtrudeMergedQuads != 0)
                        // Giải thích dòng gốc 1577: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                        AddMergedBox(x, y, rectWidth, rectHeight, color);
                    // Giải thích dòng gốc 1578: Chạy nhánh thay thế khi điều kiện if trước đó không đúng.
                    else
                        // Giải thích dòng gốc 1579: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                        AddQuad(x, y, rectWidth, rectHeight, color);
                // Giải thích dòng gốc 1580: Đóng khối code hiện tại.
                }
            // Giải thích dòng gốc 1581: Đóng khối code hiện tại.
            }
        // Giải thích dòng gốc 1582: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 1584: Khai báo hàm CanMerge với tham số trong ngoặc để thực hiện một hành vi.
        private bool CanMerge(int x, int y, Color32 color)
        // Giải thích dòng gốc 1585: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 1586: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            int localIndex = y * ChunkWidth + x;
            // Giải thích dòng gốc 1587: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            int cellIndex = (StartY + y) * GridWidth + StartX + x;
            // Giải thích dòng gốc 1588: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (Visited[localIndex] != 0 || CellSolid[cellIndex] == 0)
                // Giải thích dòng gốc 1589: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return false;

            // Giải thích dòng gốc 1591: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Color32 other = CellColors[cellIndex];
            // Giải thích dòng gốc 1592: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (MergeAnySolid != 0)
                // Giải thích dòng gốc 1593: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return true;

            // Giải thích dòng gốc 1595: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return other.r == color.r && other.g == color.g && other.b == color.b && other.a == color.a;
        // Giải thích dòng gốc 1596: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 1598: Khai báo hàm AddQuad với tham số trong ngoặc để thực hiện một hành vi.
        private void AddQuad(int x, int y, int width, int height, Color32 color)
        // Giải thích dòng gốc 1599: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 1600: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float minX = Offset.x + (StartX + x) * CellSize - CellSize * 0.5f;
            // Giải thích dòng gốc 1601: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float minY = Offset.y + (StartY + y) * CellSize - CellSize * 0.5f;
            // Giải thích dòng gốc 1602: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float maxX = minX + width * CellSize;
            // Giải thích dòng gốc 1603: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float maxY = minY + height * CellSize;
            // Giải thích dòng gốc 1604: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            int vertexIndex = Vertices.Length;

            // Giải thích dòng gốc 1606: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            Vertices.Add(new Vector3(minX, minY, 0f));
            // Giải thích dòng gốc 1607: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            Vertices.Add(new Vector3(minX, maxY, 0f));
            // Giải thích dòng gốc 1608: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            Vertices.Add(new Vector3(maxX, maxY, 0f));
            // Giải thích dòng gốc 1609: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            Vertices.Add(new Vector3(maxX, minY, 0f));

            // Giải thích dòng gốc 1611: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            AddVertexData(color);
            // Giải thích dòng gốc 1612: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            AddQuadIndices(vertexIndex);
        // Giải thích dòng gốc 1613: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 1615: Khai báo hàm AddVoxelBox với tham số trong ngoặc để thực hiện một hành vi.
        private void AddVoxelBox(int x, int y, Color32 color)
        // Giải thích dòng gốc 1616: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 1617: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float half = CellSize * DetailVoxelScale * 0.5f;
            // Giải thích dòng gốc 1618: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float halfDepth = DetailDepth * 0.5f;
            // Giải thích dòng gốc 1619: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float centerX = Offset.x + (StartX + x) * CellSize;
            // Giải thích dòng gốc 1620: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float centerY = Offset.y + (StartY + y) * CellSize;
            // Giải thích dòng gốc 1621: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float rotationY = GetRotationY(StartX + x, StartY + y);
            // Giải thích dòng gốc 1622: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 frontMin = new Vector3(centerX - half, centerY - half, -halfDepth);
            // Giải thích dòng gốc 1623: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 frontMax = new Vector3(centerX + half, centerY + half, -halfDepth);
            // Giải thích dòng gốc 1624: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 backMin = new Vector3(centerX - half, centerY - half, halfDepth);
            // Giải thích dòng gốc 1625: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 backMax = new Vector3(centerX + half, centerY + half, halfDepth);

            // Giải thích dòng gốc 1627: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            AddFace(
                // Giải thích dòng gốc 1628: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                RotateAroundCenter(new Vector3(frontMin.x, frontMin.y, frontMin.z), centerX, rotationY),
                // Giải thích dòng gốc 1629: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                RotateAroundCenter(new Vector3(frontMin.x, frontMax.y, frontMin.z), centerX, rotationY),
                // Giải thích dòng gốc 1630: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                RotateAroundCenter(new Vector3(frontMax.x, frontMax.y, frontMin.z), centerX, rotationY),
                // Giải thích dòng gốc 1631: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                RotateAroundCenter(new Vector3(frontMax.x, frontMin.y, frontMin.z), centerX, rotationY),
                // Giải thích dòng gốc 1632: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                color);
            // Giải thích dòng gốc 1633: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            AddFace(
                // Giải thích dòng gốc 1634: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                RotateAroundCenter(new Vector3(backMax.x, backMin.y, backMax.z), centerX, rotationY),
                // Giải thích dòng gốc 1635: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                RotateAroundCenter(new Vector3(backMax.x, backMax.y, backMax.z), centerX, rotationY),
                // Giải thích dòng gốc 1636: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                RotateAroundCenter(new Vector3(backMin.x, backMax.y, backMax.z), centerX, rotationY),
                // Giải thích dòng gốc 1637: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                RotateAroundCenter(new Vector3(backMin.x, backMin.y, backMax.z), centerX, rotationY),
                // Giải thích dòng gốc 1638: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                color);
            // Giải thích dòng gốc 1639: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            AddFace(
                // Giải thích dòng gốc 1640: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                RotateAroundCenter(new Vector3(frontMin.x, frontMin.y, frontMin.z), centerX, rotationY),
                // Giải thích dòng gốc 1641: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                RotateAroundCenter(new Vector3(backMin.x, backMin.y, backMin.z), centerX, rotationY),
                // Giải thích dòng gốc 1642: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                RotateAroundCenter(new Vector3(backMin.x, backMax.y, backMax.z), centerX, rotationY),
                // Giải thích dòng gốc 1643: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                RotateAroundCenter(new Vector3(frontMin.x, frontMax.y, frontMin.z), centerX, rotationY),
                // Giải thích dòng gốc 1644: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                color);
            // Giải thích dòng gốc 1645: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            AddFace(
                // Giải thích dòng gốc 1646: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                RotateAroundCenter(new Vector3(frontMax.x, frontMin.y, frontMin.z), centerX, rotationY),
                // Giải thích dòng gốc 1647: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                RotateAroundCenter(new Vector3(frontMax.x, frontMax.y, frontMin.z), centerX, rotationY),
                // Giải thích dòng gốc 1648: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                RotateAroundCenter(new Vector3(backMax.x, backMax.y, backMax.z), centerX, rotationY),
                // Giải thích dòng gốc 1649: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                RotateAroundCenter(new Vector3(backMax.x, backMin.y, backMax.z), centerX, rotationY),
                // Giải thích dòng gốc 1650: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                color);
            // Giải thích dòng gốc 1651: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            AddFace(
                // Giải thích dòng gốc 1652: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                RotateAroundCenter(new Vector3(frontMin.x, frontMax.y, frontMin.z), centerX, rotationY),
                // Giải thích dòng gốc 1653: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                RotateAroundCenter(new Vector3(backMin.x, backMax.y, backMax.z), centerX, rotationY),
                // Giải thích dòng gốc 1654: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                RotateAroundCenter(new Vector3(backMax.x, backMax.y, backMax.z), centerX, rotationY),
                // Giải thích dòng gốc 1655: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                RotateAroundCenter(new Vector3(frontMax.x, frontMax.y, frontMin.z), centerX, rotationY),
                // Giải thích dòng gốc 1656: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                color);
            // Giải thích dòng gốc 1657: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            AddFace(
                // Giải thích dòng gốc 1658: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                RotateAroundCenter(new Vector3(frontMin.x, frontMin.y, frontMin.z), centerX, rotationY),
                // Giải thích dòng gốc 1659: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                RotateAroundCenter(new Vector3(frontMax.x, frontMin.y, frontMin.z), centerX, rotationY),
                // Giải thích dòng gốc 1660: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                RotateAroundCenter(new Vector3(backMax.x, backMin.y, backMax.z), centerX, rotationY),
                // Giải thích dòng gốc 1661: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                RotateAroundCenter(new Vector3(backMin.x, backMin.y, backMin.z), centerX, rotationY),
                // Giải thích dòng gốc 1662: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                color);
        // Giải thích dòng gốc 1663: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 1665: Khai báo hàm AddMergedBox với tham số trong ngoặc để thực hiện một hành vi.
        private void AddMergedBox(int x, int y, int width, int height, Color32 color)
        // Giải thích dòng gốc 1666: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 1667: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float minX = Offset.x + (StartX + x) * CellSize - CellSize * 0.5f;
            // Giải thích dòng gốc 1668: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float minY = Offset.y + (StartY + y) * CellSize - CellSize * 0.5f;
            // Giải thích dòng gốc 1669: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float maxX = minX + width * CellSize;
            // Giải thích dòng gốc 1670: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float maxY = minY + height * CellSize;
            // Giải thích dòng gốc 1671: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float halfDepth = DetailDepth * 0.5f;

            // Giải thích dòng gốc 1673: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 frontMin = new Vector3(minX, minY, -halfDepth);
            // Giải thích dòng gốc 1674: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 frontMax = new Vector3(maxX, maxY, -halfDepth);
            // Giải thích dòng gốc 1675: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 backMin = new Vector3(minX, minY, halfDepth);
            // Giải thích dòng gốc 1676: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 backMax = new Vector3(maxX, maxY, halfDepth);

            // Giải thích dòng gốc 1678: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            AddFace(new Vector3(frontMin.x, frontMin.y, frontMin.z), new Vector3(frontMin.x, frontMax.y, frontMin.z),
                // Giải thích dòng gốc 1679: Tạo một instance mới của kiểu dữ liệu được chỉ định.
                new Vector3(frontMax.x, frontMax.y, frontMin.z), new Vector3(frontMax.x, frontMin.y, frontMin.z),
                // Giải thích dòng gốc 1680: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                color);
            // Giải thích dòng gốc 1681: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            AddFace(new Vector3(backMax.x, backMin.y, backMax.z), new Vector3(backMax.x, backMax.y, backMax.z),
                // Giải thích dòng gốc 1682: Tạo một instance mới của kiểu dữ liệu được chỉ định.
                new Vector3(backMin.x, backMax.y, backMax.z), new Vector3(backMin.x, backMin.y, backMax.z), color);
            // Giải thích dòng gốc 1683: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            AddFace(new Vector3(frontMin.x, frontMin.y, frontMin.z), new Vector3(backMin.x, backMin.y, backMin.z),
                // Giải thích dòng gốc 1684: Tạo một instance mới của kiểu dữ liệu được chỉ định.
                new Vector3(backMin.x, backMax.y, backMax.z), new Vector3(frontMin.x, frontMax.y, frontMin.z),
                // Giải thích dòng gốc 1685: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                color);
            // Giải thích dòng gốc 1686: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            AddFace(new Vector3(frontMax.x, frontMin.y, frontMin.z), new Vector3(frontMax.x, frontMax.y, frontMin.z),
                // Giải thích dòng gốc 1687: Tạo một instance mới của kiểu dữ liệu được chỉ định.
                new Vector3(backMax.x, backMax.y, backMax.z), new Vector3(backMax.x, backMin.y, backMax.z), color);
            // Giải thích dòng gốc 1688: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            AddFace(new Vector3(frontMin.x, frontMax.y, frontMin.z), new Vector3(backMin.x, backMax.y, backMax.z),
                // Giải thích dòng gốc 1689: Tạo một instance mới của kiểu dữ liệu được chỉ định.
                new Vector3(backMax.x, backMax.y, backMax.z), new Vector3(frontMax.x, frontMax.y, frontMin.z),
                // Giải thích dòng gốc 1690: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                color);
            // Giải thích dòng gốc 1691: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            AddFace(new Vector3(frontMin.x, frontMin.y, frontMin.z), new Vector3(frontMax.x, frontMin.y, frontMin.z),
                // Giải thích dòng gốc 1692: Tạo một instance mới của kiểu dữ liệu được chỉ định.
                new Vector3(backMax.x, backMin.y, backMax.z), new Vector3(backMin.x, backMin.y, backMin.z), color);
        // Giải thích dòng gốc 1693: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 1695: Khai báo hàm GetRotationY với tham số trong ngoặc để thực hiện một hành vi.
        private float GetRotationY(int x, int y)
        // Giải thích dòng gốc 1696: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 1697: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (RandomYRotation <= 0f)
                // Giải thích dòng gốc 1698: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return 0f;

            // Giải thích dòng gốc 1700: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            uint hash = (uint)(x * 73856093) ^ (uint)(y * 19349663);
            // Giải thích dòng gốc 1701: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float normalized = (hash & 1023u) * (1f / 1023f);
            // Giải thích dòng gốc 1702: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return (normalized * 2f - 1f) * RandomYRotation;
        // Giải thích dòng gốc 1703: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 1705: Khai báo hàm RotateAroundCenter với tham số trong ngoặc để thực hiện một hành vi.
        private Vector3 RotateAroundCenter(Vector3 point, float centerX, float degrees)
        // Giải thích dòng gốc 1706: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 1707: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (degrees == 0f)
                // Giải thích dòng gốc 1708: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return point;

            // Giải thích dòng gốc 1710: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float radians = degrees * 0.0174532924f;
            // Giải thích dòng gốc 1711: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float sin = Mathf.Sin(radians);
            // Giải thích dòng gốc 1712: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float cos = Mathf.Cos(radians);
            // Giải thích dòng gốc 1713: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float localX = point.x - centerX;
            // Giải thích dòng gốc 1714: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float rotatedX = localX * cos + point.z * sin;
            // Giải thích dòng gốc 1715: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float rotatedZ = -localX * sin + point.z * cos;
            // Giải thích dòng gốc 1716: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return new Vector3(centerX + rotatedX, point.y, rotatedZ);
        // Giải thích dòng gốc 1717: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 1719: Khai báo hàm AddFace với tham số trong ngoặc để thực hiện một hành vi.
        private void AddFace(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 color)
        // Giải thích dòng gốc 1720: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 1721: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            int vertexIndex = Vertices.Length;

            // Giải thích dòng gốc 1723: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            Vertices.Add(a);
            // Giải thích dòng gốc 1724: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            Vertices.Add(b);
            // Giải thích dòng gốc 1725: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            Vertices.Add(c);
            // Giải thích dòng gốc 1726: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            Vertices.Add(d);

            // Giải thích dòng gốc 1728: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            AddVertexData(color);
            // Giải thích dòng gốc 1729: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            AddQuadIndices(vertexIndex);
        // Giải thích dòng gốc 1730: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 1732: Khai báo hàm AddVertexData với tham số trong ngoặc để thực hiện một hành vi.
        private void AddVertexData(Color32 color)
        // Giải thích dòng gốc 1733: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 1734: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            Colors.Add(color);
            // Giải thích dòng gốc 1735: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            Colors.Add(color);
            // Giải thích dòng gốc 1736: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            Colors.Add(color);
            // Giải thích dòng gốc 1737: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            Colors.Add(color);

            // Giải thích dòng gốc 1739: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            Uvs.Add(new Vector2(0f, 0f));
            // Giải thích dòng gốc 1740: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            Uvs.Add(new Vector2(0f, 1f));
            // Giải thích dòng gốc 1741: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            Uvs.Add(new Vector2(1f, 1f));
            // Giải thích dòng gốc 1742: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            Uvs.Add(new Vector2(1f, 0f));
        // Giải thích dòng gốc 1743: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 1745: Khai báo hàm AddQuadIndices với tham số trong ngoặc để thực hiện một hành vi.
        private void AddQuadIndices(int vertexIndex)
        // Giải thích dòng gốc 1746: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 1747: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            Indices.Add(vertexIndex);
            // Giải thích dòng gốc 1748: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            Indices.Add(vertexIndex + 1);
            // Giải thích dòng gốc 1749: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            Indices.Add(vertexIndex + 2);
            // Giải thích dòng gốc 1750: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            Indices.Add(vertexIndex);
            // Giải thích dòng gốc 1751: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            Indices.Add(vertexIndex + 2);
            // Giải thích dòng gốc 1752: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            Indices.Add(vertexIndex + 3);
        // Giải thích dòng gốc 1753: Đóng khối code hiện tại.
        }
    // Giải thích dòng gốc 1754: Đóng khối code hiện tại.
    }
// Giải thích dòng gốc 1755: Đóng khối code hiện tại.
}
