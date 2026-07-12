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
        // Giải thích dòng gốc 7: Khai báo hàm AllocateSolverBuffers với tham số trong ngoặc để thực hiện một hành vi.
        private void AllocateSolverBuffers()
        // Giải thích dòng gốc 8: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 9: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _solvePositions = new Vector3[_maxJointCount + 1];
            // Giải thích dòng gốc 10: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _segmentLengths = new float[_maxJointCount];
            // Giải thích dòng gốc 11: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _jointRotationOffsets = new Quaternion[_maxJointCount];
        // Giải thích dòng gốc 12: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 14: Khai báo hàm CacheModelPoseOffsets với tham số trong ngoặc để thực hiện một hành vi.
        private void CacheModelPoseOffsets()
        // Giải thích dòng gốc 15: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 16: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            RefreshActiveSegmentData();
        // Giải thích dòng gốc 17: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 19: Khai báo hàm CacheFixedRoot với tham số trong ngoặc để thực hiện một hành vi.
        private void CacheFixedRoot()
        // Giải thích dòng gốc 20: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 21: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Transform root = _joints.Count > 0 ? _joints[0] : null;
            // Giải thích dòng gốc 22: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (root == null)
                // Giải thích dòng gốc 23: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return;

            // Giải thích dòng gốc 25: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _rootParent = root.parent;
            // Giải thích dòng gốc 26: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _rootLocalPosition = root.localPosition;
        // Giải thích dòng gốc 27: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 29: Khai báo hàm DetachSawFromJoints với tham số trong ngoặc để thực hiện một hành vi.
        private void DetachSawFromJoints()
        // Giải thích dòng gốc 30: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 31: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (_saw != null && _saw.parent != transform)
                // Giải thích dòng gốc 32: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                _saw.SetParent(transform, true);
        // Giải thích dòng gốc 33: Đóng khối code hiện tại.
        }
    // Giải thích dòng gốc 34: Đóng khối code hiện tại.
    }
// Giải thích dòng gốc 35: Đóng khối code hiện tại.
}
