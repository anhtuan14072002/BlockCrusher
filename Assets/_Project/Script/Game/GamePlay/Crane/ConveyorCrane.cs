// Giải thích dòng gốc 1: Nạp namespace UnityEngine để file dùng được các kiểu và API trong đó.
using UnityEngine;

// Giải thích dòng gốc 3: Gắn attribute [RequireComponent(typeof(BoxCollider))] cho khai báo nằm ngay bên dưới.
[RequireComponent(typeof(BoxCollider))]
// Giải thích dòng gốc 4: Khai báo class ConveyorCrane để chứa dữ liệu và hành vi liên quan.
public sealed class ConveyorCrane : MonoBehaviour
// Giải thích dòng gốc 5: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
{
    // Giải thích dòng gốc 6: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField] private Vector3 _worldDirection = Vector3.right;
    // Giải thích dòng gốc 7: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField] private float _speed = 1.5f;
    // Giải thích dòng gốc 8: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField] private float _acceleration = 12f;

    // Giải thích dòng gốc 10: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private Vector3 _direction;
    // Giải thích dòng gốc 11: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private BoxCollider _conveyorCollider;

    // Giải thích dòng gốc 13: Khai báo hàm Awake với tham số trong ngoặc để thực hiện một hành vi.
    private void Awake()
    // Giải thích dòng gốc 14: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 15: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        CacheDirection();

        // Giải thích dòng gốc 17: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _conveyorCollider = GetComponent<BoxCollider>();
        // Giải thích dòng gốc 18: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _conveyorCollider.isTrigger = true;
    // Giải thích dòng gốc 19: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 21: Khai báo hàm OnValidate với tham số trong ngoặc để thực hiện một hành vi.
    private void OnValidate()
    // Giải thích dòng gốc 22: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 23: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        CacheDirection();

        // Giải thích dòng gốc 25: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        Collider conveyorCollider = GetComponent<Collider>();
        // Giải thích dòng gốc 26: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (conveyorCollider != null)
            // Giải thích dòng gốc 27: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            conveyorCollider.isTrigger = true;
    // Giải thích dòng gốc 28: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 30: Khai báo hàm FixedUpdate với tham số trong ngoặc để thực hiện một hành vi.
    private void FixedUpdate()
    // Giải thích dòng gốc 31: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 32: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_conveyorCollider != null)
            // Giải thích dòng gốc 33: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            TextureBlockSpawner.ApplyConveyorForActiveSpawners(_conveyorCollider.bounds, _direction, _speed,
                // Giải thích dòng gốc 34: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                _acceleration, Time.fixedDeltaTime);
    // Giải thích dòng gốc 35: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 37: Khai báo hàm CacheDirection với tham số trong ngoặc để thực hiện một hành vi.
    private void CacheDirection()
    // Giải thích dòng gốc 38: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 39: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _direction = _worldDirection.sqrMagnitude > 0.0001f ? _worldDirection.normalized : Vector3.right;
    // Giải thích dòng gốc 40: Đóng khối code hiện tại.
    }

// Giải thích dòng gốc 42: Đóng khối code hiện tại.
}
