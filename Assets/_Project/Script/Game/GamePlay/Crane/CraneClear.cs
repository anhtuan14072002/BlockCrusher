// Giải thích dòng gốc 1: Nạp namespace UnityEngine để file dùng được các kiểu và API trong đó.
using UnityEngine;

// Giải thích dòng gốc 3: Gắn attribute [RequireComponent(typeof(BoxCollider))] cho khai báo nằm ngay bên dưới.
[RequireComponent(typeof(BoxCollider))]
// Giải thích dòng gốc 4: Khai báo class CraneClear để chứa dữ liệu và hành vi liên quan.
public sealed class CraneClear : MonoBehaviour
// Giải thích dòng gốc 5: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
{
    // Giải thích dòng gốc 6: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private BoxCollider _clearCollider;

    // Giải thích dòng gốc 8: Khai báo hàm Awake với tham số trong ngoặc để thực hiện một hành vi.
    private void Awake()
    // Giải thích dòng gốc 9: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 10: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _clearCollider = GetComponent<BoxCollider>();
        // Giải thích dòng gốc 11: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _clearCollider.isTrigger = true;
    // Giải thích dòng gốc 12: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 14: Khai báo hàm OnValidate với tham số trong ngoặc để thực hiện một hành vi.
    private void OnValidate()
    // Giải thích dòng gốc 15: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 16: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        BoxCollider clearCollider = GetComponent<BoxCollider>();
        // Giải thích dòng gốc 17: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (clearCollider != null)
            // Giải thích dòng gốc 18: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            clearCollider.isTrigger = true;
    // Giải thích dòng gốc 19: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 21: Khai báo hàm FixedUpdate với tham số trong ngoặc để thực hiện một hành vi.
    private void FixedUpdate()
    // Giải thích dòng gốc 22: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 23: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_clearCollider != null)
            // Giải thích dòng gốc 24: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            TextureBlockSpawner.ClearReleasedBlocksForActiveSpawners(_clearCollider.bounds);
    // Giải thích dòng gốc 25: Đóng khối code hiện tại.
    }
// Giải thích dòng gốc 26: Đóng khối code hiện tại.
}
