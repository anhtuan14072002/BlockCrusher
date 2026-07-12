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
        // Giải thích dòng gốc 7: Khai báo hàm GetRootPosition với tham số trong ngoặc để thực hiện một hành vi.
        private Vector3 GetRootPosition()
        // Giải thích dòng gốc 8: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 9: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (_rootParent != null)
                // Giải thích dòng gốc 10: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return _rootParent.TransformPoint(_rootLocalPosition);

            // Giải thích dòng gốc 12: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return _joints.Count > 0 && _joints[0] != null ? _joints[0].position : transform.position;
        // Giải thích dòng gốc 13: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 15: Khai báo hàm ClampSawTargetToReach với tham số trong ngoặc để thực hiện một hành vi.
        private void ClampSawTargetToReach()
        // Giải thích dòng gốc 16: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 17: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 rootPosition = GetRootPosition();
            // Giải thích dòng gốc 18: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 offset = _sawTarget - rootPosition;
            // Giải thích dòng gốc 19: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float maxReach = GetActiveReach();

            // Giải thích dòng gốc 21: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (offset.sqrMagnitude > maxReach * maxReach)
                // Giải thích dòng gốc 22: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                _sawTarget = rootPosition + offset.normalized * maxReach;
        // Giải thích dòng gốc 23: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 25: Khai báo hàm GetSawPosition với tham số trong ngoặc để thực hiện một hành vi.
        private Vector3 GetSawPosition()
        // Giải thích dòng gốc 26: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 27: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return _saw != null ? _saw.position : transform.position;
        // Giải thích dòng gốc 28: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 30: Khai báo hàm CacheMovementCamera với tham số trong ngoặc để thực hiện một hành vi.
        private void CacheMovementCamera()
        // Giải thích dòng gốc 31: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 32: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (_movementCamera == null)
                // Giải thích dòng gốc 33: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                _movementCamera = Camera.main;

            // Giải thích dòng gốc 35: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (_movementCamera != null)
                // Giải thích dòng gốc 36: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                _movementCameraTransform = _movementCamera.transform;
        // Giải thích dòng gốc 37: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 39: Khai báo hàm GetSawMoveDirection với tham số trong ngoặc để thực hiện một hành vi.
        private Vector3 GetSawMoveDirection(Vector2 input)
        // Giải thích dòng gốc 40: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 41: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (_movementCameraTransform == null)
                // Giải thích dòng gốc 42: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return new Vector3(input.x, input.y, 0f);

            // Giải thích dòng gốc 44: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 screenRight = _movementCameraTransform.right;
            // Giải thích dòng gốc 45: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            screenRight.z = 0f;

            // Giải thích dòng gốc 47: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 screenUp = _movementCameraTransform.up;
            // Giải thích dòng gốc 48: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            screenUp.z = 0f;

            // Giải thích dòng gốc 50: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (screenRight.sqrMagnitude <= 0.0001f || screenUp.sqrMagnitude <= 0.0001f)
                // Giải thích dòng gốc 51: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return new Vector3(input.x, input.y, 0f);

            // Giải thích dòng gốc 53: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 moveDirection = screenRight.normalized * input.x + screenUp.normalized * input.y;
            // Giải thích dòng gốc 54: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (moveDirection.sqrMagnitude > 1f)
                // Giải thích dòng gốc 55: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                moveDirection.Normalize();

            // Giải thích dòng gốc 57: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return moveDirection;
        // Giải thích dòng gốc 58: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 60: Khai báo hàm CacheSawCutter với tham số trong ngoặc để thực hiện một hành vi.
        private void CacheSawCutter()
        // Giải thích dòng gốc 61: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 62: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (_sawCutter == null && _saw != null)
                // Giải thích dòng gốc 63: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                _sawCutter = _saw.GetComponent<SawBlockCutter>();
        // Giải thích dòng gốc 64: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 66: Khai báo hàm GetActiveReach với tham số trong ngoặc để thực hiện một hành vi.
        private float GetActiveReach()
        // Giải thích dòng gốc 67: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 68: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return _activeReach;
        // Giải thích dòng gốc 69: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 71: Khai báo hàm GetSegmentRotation với tham số trong ngoặc để thực hiện một hành vi.
        private static Quaternion GetSegmentRotation(Vector3 direction)
        // Giải thích dòng gốc 72: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 73: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        // Giải thích dòng gốc 74: Đóng khối code hiện tại.
        }
    // Giải thích dòng gốc 75: Đóng khối code hiện tại.
    }
// Giải thích dòng gốc 76: Đóng khối code hiện tại.
}
