// Giải thích dòng gốc 1: Nạp namespace UnityEngine để file dùng được các kiểu và API trong đó.
using UnityEngine;

// Giải thích dòng gốc 3: Khai báo namespace Crusher để gom nhóm code theo module.
namespace Crusher
// Giải thích dòng gốc 4: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
{
    // Giải thích dòng gốc 5: Khai báo partial class CraneController, cho phép chia class này qua nhiều file.
    public sealed partial class CraneController
    // Giải thích dòng gốc 6: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 7: Khai báo hàm MoveSawTarget với tham số trong ngoặc để thực hiện một hành vi.
        private void MoveSawTarget(Vector2 input)
        // Giải thích dòng gốc 8: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 9: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float moveMultiplier = _sawCutter != null && _sawCutter.IsResisting ? _sawContactMoveMultiplier : 1f;
            // Giải thích dòng gốc 10: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 moveDirection = GetSawMoveDirection(input);
            // Giải thích dòng gốc 11: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _lastSawMoveDirection = moveDirection;
            // Giải thích dòng gốc 12: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _sawTarget += moveDirection * (_sawMoveSpeed * moveMultiplier * Time.deltaTime);
            // Giải thích dòng gốc 13: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _sawTarget.x = Mathf.Clamp(_sawTarget.x, _targetXBounds.x, _targetXBounds.y);
            // Giải thích dòng gốc 14: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _sawTarget.y = Mathf.Clamp(_sawTarget.y, _targetYBounds.x, _targetYBounds.y);
            // Giải thích dòng gốc 15: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            ClampSawTargetToReach();

            // Giải thích dòng gốc 17: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (_isSuctionMode && _suctionDevice != null)
                // Giải thích dòng gốc 18: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                _sawTarget = _suctionDevice.ClampSawTarget(GetSawPosition(), _sawTarget);
        // Giải thích dòng gốc 19: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 21: Khai báo hàm ApplySawAtPosition với tham số trong ngoặc để thực hiện một hành vi.
        private void ApplySawAtPosition(Vector3 sawPosition)
        // Giải thích dòng gốc 22: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 23: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (_saw == null || _activeJointCount == 0)
                // Giải thích dòng gốc 24: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return;

            // Giải thích dòng gốc 26: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _sawTarget = sawPosition;
            // Giải thích dòng gốc 27: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _saw.position = sawPosition;

            // Giải thích dòng gốc 29: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Transform lastJoint = _joints[_activeJointCount - 1];
            // Giải thích dòng gốc 30: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (lastJoint == null)
                // Giải thích dòng gốc 31: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return;

            // Giải thích dòng gốc 33: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 sawDirection = sawPosition - lastJoint.position;
            // Giải thích dòng gốc 34: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (sawDirection.sqrMagnitude > 0.0001f)
                // Giải thích dòng gốc 35: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                _saw.rotation = GetSegmentRotation(sawDirection) * _sawRotationOffset;
        // Giải thích dòng gốc 36: Đóng khối code hiện tại.
        }
    // Giải thích dòng gốc 37: Đóng khối code hiện tại.
    }
// Giải thích dòng gốc 38: Đóng khối code hiện tại.
}
