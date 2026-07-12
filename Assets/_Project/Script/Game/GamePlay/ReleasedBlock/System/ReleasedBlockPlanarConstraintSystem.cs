// Giải thích dòng gốc 1: Nạp namespace Unity.Burst để file dùng được các kiểu và API trong đó.
using Unity.Burst;
// Giải thích dòng gốc 2: Nạp namespace Unity.Entities để file dùng được các kiểu và API trong đó.
using Unity.Entities;
// Giải thích dòng gốc 3: Nạp namespace Unity.Mathematics để file dùng được các kiểu và API trong đó.
using Unity.Mathematics;
// Giải thích dòng gốc 4: Nạp namespace Unity.Physics để file dùng được các kiểu và API trong đó.
using Unity.Physics;
// Giải thích dòng gốc 5: Nạp namespace Unity.Physics.Systems để file dùng được các kiểu và API trong đó.
using Unity.Physics.Systems;
// Giải thích dòng gốc 6: Nạp namespace Unity.Transforms để file dùng được các kiểu và API trong đó.
using Unity.Transforms;

// Giải thích dòng gốc 8: Gắn attribute [BurstCompile] cho khai báo nằm ngay bên dưới.
[BurstCompile]
// Giải thích dòng gốc 9: Gắn attribute [UpdateInGroup(typeof(AfterPhysicsSystemGroup))] cho khai báo nằm ngay bên dưới.
[UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
// Giải thích dòng gốc 10: Khai báo struct ReleasedBlockPlanarConstraintSystem là kiểu dữ liệu value type.
public partial struct ReleasedBlockPlanarConstraintSystem : ISystem
// Giải thích dòng gốc 11: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
{
    // Giải thích dòng gốc 12: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private EntityQuery _query;

    // Giải thích dòng gốc 14: Khai báo hàm OnCreate với tham số trong ngoặc để thực hiện một hành vi.
    public void OnCreate(ref SystemState state)
    // Giải thích dòng gốc 15: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 16: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _query = SystemAPI.QueryBuilder()
            // Giải thích dòng gốc 17: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
            .WithAllRW<LocalTransform, PhysicsVelocity>()
            // Giải thích dòng gốc 18: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
            .WithAllRW<PhysicsGravityFactor, ReleasedBlockComponent>()
            // Giải thích dòng gốc 19: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
            .Build();
        // Giải thích dòng gốc 20: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        state.RequireForUpdate(_query);
    // Giải thích dòng gốc 21: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 23: Gắn attribute [BurstCompile] cho khai báo nằm ngay bên dưới.
    [BurstCompile]
    // Giải thích dòng gốc 24: Khai báo hàm OnUpdate với tham số trong ngoặc để thực hiện một hành vi.
    public void OnUpdate(ref SystemState state)
    // Giải thích dòng gốc 25: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 26: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        state.Dependency = new PlanarConstraintJob().ScheduleParallel(_query, state.Dependency);
    // Giải thích dòng gốc 27: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 29: Gắn attribute [BurstCompile] cho khai báo nằm ngay bên dưới.
    [BurstCompile]
    // Giải thích dòng gốc 30: Khai báo struct PlanarConstraintJob là kiểu dữ liệu value type.
    private partial struct PlanarConstraintJob : IJobEntity
    // Giải thích dòng gốc 31: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 32: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
        private void Execute(ref LocalTransform transform, ref PhysicsVelocity velocity,
            // Giải thích dòng gốc 33: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
            ref PhysicsGravityFactor gravity, ref ReleasedBlockComponent block)
        // Giải thích dòng gốc 34: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 35: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            transform.Position.z = block.LockedZ;
            // Giải thích dòng gốc 36: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            velocity.Linear.z = 0f;
            // Giải thích dòng gốc 37: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            velocity.Angular = float3.zero;

            // Giải thích dòng gốc 39: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float speedSq = math.lengthsq(velocity.Linear.xy);
            // Giải thích dòng gốc 40: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (speedSq < ReleasedBlockComponent.SettleSpeed * ReleasedBlockComponent.SettleSpeed)
            // Giải thích dòng gốc 41: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
            {
                // Giải thích dòng gốc 42: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                velocity.Linear.xy = float2.zero;
                // Giải thích dòng gốc 43: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
                if (block.StableFrames < ReleasedBlockComponent.SettleFrames)
                    // Giải thích dòng gốc 44: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                    block.StableFrames++;

                // Giải thích dòng gốc 46: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
                if (block.StableFrames >= ReleasedBlockComponent.SettleFrames)
                    // Giải thích dòng gốc 47: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                    gravity.Value = 0f;

                // Giải thích dòng gốc 49: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return;
            // Giải thích dòng gốc 50: Đóng khối code hiện tại.
            }

            // Giải thích dòng gốc 52: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            block.StableFrames = 0;
            // Giải thích dòng gốc 53: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            gravity.Value = 1f;

            // Giải thích dòng gốc 55: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float maxSpeedSq = block.MaxPlanarSpeed * block.MaxPlanarSpeed;
            // Giải thích dòng gốc 56: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (speedSq > maxSpeedSq)
            // Giải thích dòng gốc 57: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
            {
                // Giải thích dòng gốc 58: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                float2 planar = math.normalize(velocity.Linear.xy) * block.MaxPlanarSpeed;
                // Giải thích dòng gốc 59: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                velocity.Linear.x = planar.x;
                // Giải thích dòng gốc 60: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                velocity.Linear.y = planar.y;
            // Giải thích dòng gốc 61: Đóng khối code hiện tại.
            }
        // Giải thích dòng gốc 62: Đóng khối code hiện tại.
        }
    // Giải thích dòng gốc 63: Đóng khối code hiện tại.
    }
// Giải thích dòng gốc 64: Đóng khối code hiện tại.
}
