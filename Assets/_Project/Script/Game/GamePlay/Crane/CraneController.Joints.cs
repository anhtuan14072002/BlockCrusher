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
        // Giải thích dòng gốc 7: Khai báo hàm ApplyActiveJointCount với tham số trong ngoặc để thực hiện một hành vi.
        private void ApplyActiveJointCount()
        // Giải thích dòng gốc 8: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 9: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
            for (int i = 0; i < _joints.Count; i++)
            // Giải thích dòng gốc 10: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
            {
                // Giải thích dòng gốc 11: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
                if (_joints[i] != null)
                    // Giải thích dòng gốc 12: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                    _joints[i].gameObject.SetActive(i < _activeJointCount);
            // Giải thích dòng gốc 13: Đóng khối code hiện tại.
            }

            // Giải thích dòng gốc 15: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            RefreshActiveReach();
        // Giải thích dòng gốc 16: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 18: Khai báo hàm RefreshActiveSegmentData với tham số trong ngoặc để thực hiện một hành vi.
        private void RefreshActiveSegmentData()
        // Giải thích dòng gốc 19: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 20: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
            for (int i = 0; i < _activeJointCount; i++)
            // Giải thích dòng gốc 21: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
            {
                // Giải thích dòng gốc 22: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                Transform joint = _joints[i];
                // Giải thích dòng gốc 23: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
                if (joint == null)
                    // Giải thích dòng gốc 24: Bỏ qua phần còn lại của lần lặp hiện tại và sang lần tiếp theo.
                    continue;

                // Giải thích dòng gốc 26: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                Vector3 direction = GetSegmentEndPosition(i) - joint.position;
                // Giải thích dòng gốc 27: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
                if (direction.sqrMagnitude <= 0.0001f)
                    // Giải thích dòng gốc 28: Bỏ qua phần còn lại của lần lặp hiện tại và sang lần tiếp theo.
                    continue;

                // Giải thích dòng gốc 30: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                _segmentLengths[i] = direction.magnitude;
                // Giải thích dòng gốc 31: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                _jointRotationOffsets[i] = Quaternion.Inverse(GetSegmentRotation(direction)) * joint.rotation;
            // Giải thích dòng gốc 32: Đóng khối code hiện tại.
            }

            // Giải thích dòng gốc 34: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            CacheSawRotationOffset();
            // Giải thích dòng gốc 35: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            RefreshActiveReach();
        // Giải thích dòng gốc 36: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 38: Khai báo hàm RefreshActiveReach với tham số trong ngoặc để thực hiện một hành vi.
        private void RefreshActiveReach()
        // Giải thích dòng gốc 39: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 40: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float reach = 0f;
            // Giải thích dòng gốc 41: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
            for (int i = 0; i < _activeJointCount; i++)
                // Giải thích dòng gốc 42: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                reach += _segmentLengths[i];

            // Giải thích dòng gốc 44: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _activeReach = reach;
        // Giải thích dòng gốc 45: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 47: Khai báo hàm CacheSawRotationOffset với tham số trong ngoặc để thực hiện một hành vi.
        private void CacheSawRotationOffset()
        // Giải thích dòng gốc 48: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 49: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (_saw == null || _activeJointCount == 0)
                // Giải thích dòng gốc 50: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return;

            // Giải thích dòng gốc 52: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Transform lastJoint = _joints[_activeJointCount - 1];
            // Giải thích dòng gốc 53: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (lastJoint == null)
                // Giải thích dòng gốc 54: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return;

            // Giải thích dòng gốc 56: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 direction = _saw.position - lastJoint.position;
            // Giải thích dòng gốc 57: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (direction.sqrMagnitude > 0.0001f)
                // Giải thích dòng gốc 58: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                _sawRotationOffset = Quaternion.Inverse(GetSegmentRotation(direction)) * _saw.rotation;
        // Giải thích dòng gốc 59: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 61: Khai báo hàm GetSegmentEndPosition với tham số trong ngoặc để thực hiện một hành vi.
        private Vector3 GetSegmentEndPosition(int jointIndex)
        // Giải thích dòng gốc 62: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 63: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (jointIndex == _activeJointCount - 1 && _saw != null)
                // Giải thích dòng gốc 64: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return _saw.position;

            // Giải thích dòng gốc 66: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            int nextJointIndex = jointIndex + 1;
            // Giải thích dòng gốc 67: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (nextJointIndex < _joints.Count && _joints[nextJointIndex] != null)
                // Giải thích dòng gốc 68: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return _joints[nextJointIndex].position;

            // Giải thích dòng gốc 70: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (_saw != null)
                // Giải thích dòng gốc 71: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return _saw.position;

            // Giải thích dòng gốc 73: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return _joints[jointIndex].position + _joints[jointIndex].right * _segmentLength;
        // Giải thích dòng gốc 74: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 76: Khai báo hàm GetOrCreateJoint với tham số trong ngoặc để thực hiện một hành vi.
        private Transform GetOrCreateJoint(int jointIndex)
        // Giải thích dòng gốc 77: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 78: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            EnsureJointSlot(jointIndex);

            // Giải thích dòng gốc 80: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (_joints[jointIndex] != null)
                // Giải thích dòng gốc 81: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return _joints[jointIndex];

            // Giải thích dòng gốc 83: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Transform joint = CreateJointFromPrefab(jointIndex);
            // Giải thích dòng gốc 84: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (joint == null)
                // Giải thích dòng gốc 85: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return null;

            // Giải thích dòng gốc 87: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            joint.name = "Joint_" + jointIndex;
            // Giải thích dòng gốc 88: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            RenameGeneratedJointChildren(joint, jointIndex);
            // Giải thích dòng gốc 89: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return joint;
        // Giải thích dòng gốc 90: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 92: Khai báo hàm EnsureJointSlot với tham số trong ngoặc để thực hiện một hành vi.
        private void EnsureJointSlot(int jointIndex)
        // Giải thích dòng gốc 93: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 94: Bắt đầu vòng lặp while và tiếp tục lặp khi điều kiện còn đúng.
            while (_joints.Count <= jointIndex)
                // Giải thích dòng gốc 95: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                _joints.Add(null);
        // Giải thích dòng gốc 96: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 98: Khai báo hàm CreateJointFromPrefab với tham số trong ngoặc để thực hiện một hành vi.
        private Transform CreateJointFromPrefab(int jointIndex)
        // Giải thích dòng gốc 99: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 100: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (_jointPrefab != null)
                // Giải thích dòng gốc 101: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return Instantiate(_jointPrefab, transform).transform;

            // Giải thích dòng gốc 103: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Transform source = _joints[jointIndex - 1];
            // Giải thích dòng gốc 104: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return source != null ? Instantiate(source, transform) : null;
        // Giải thích dòng gốc 105: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 107: Khai báo hàm InsertJointBeforeLast với tham số trong ngoặc để thực hiện một hành vi.
        private void InsertJointBeforeLast(int insertIndex, int storageIndex, Transform insertedJoint)
        // Giải thích dòng gốc 108: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 109: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Transform pushedJoint = _joints[insertIndex];
            // Giải thích dòng gốc 110: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (pushedJoint == null || insertedJoint == null || _saw == null)
                // Giải thích dòng gốc 111: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return;

            // Giải thích dòng gốc 113: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 pushedPosition = pushedJoint.position;
            // Giải thích dòng gốc 114: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 sawPosition = _saw.position;
            // Giải thích dòng gốc 115: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 direction = sawPosition - pushedPosition;
            // Giải thích dòng gốc 116: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float segmentLength = direction.magnitude;
            // Giải thích dòng gốc 117: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (segmentLength <= 0.0001f)
                // Giải thích dòng gốc 118: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                segmentLength = _segmentLengths[insertIndex] > 0.0001f ? _segmentLengths[insertIndex] : _segmentLength;

            // Giải thích dòng gốc 120: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            insertedJoint.gameObject.SetActive(true);
            // Giải thích dòng gốc 121: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            insertedJoint.position = pushedPosition;
            // Giải thích dòng gốc 122: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            insertedJoint.rotation = pushedJoint.rotation;

            // Giải thích dòng gốc 124: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            pushedJoint.position = sawPosition;
            // Giải thích dòng gốc 125: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            pushedJoint.rotation = direction.sqrMagnitude > 0.0001f
                // Giải thích dòng gốc 126: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                ? GetSegmentRotation(direction) * _jointRotationOffsets[insertIndex]
                // Giải thích dòng gốc 127: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                : pushedJoint.rotation;

            // Giải thích dòng gốc 129: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _segmentLengths[storageIndex] = segmentLength;
            // Giải thích dòng gốc 130: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _jointRotationOffsets[storageIndex] = _jointRotationOffsets[insertIndex];

            // Giải thích dòng gốc 132: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _sawTarget = sawPosition;
            // Giải thích dòng gốc 133: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _joints[storageIndex] = pushedJoint;
            // Giải thích dòng gốc 134: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _joints[insertIndex] = insertedJoint;
        // Giải thích dòng gốc 135: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 137: Khai báo hàm RenameGeneratedJointChildren với tham số trong ngoặc để thực hiện một hành vi.
        private static void RenameGeneratedJointChildren(Transform joint, int jointIndex)
        // Giải thích dòng gốc 138: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 139: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
            for (int i = 0; i < joint.childCount; i++)
            // Giải thích dòng gốc 140: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
            {
                // Giải thích dòng gốc 141: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                Transform child = joint.GetChild(i);
                // Giải thích dòng gốc 142: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
                if (child.name.StartsWith("JointBlock_"))
                    // Giải thích dòng gốc 143: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                    child.name = "JointBlock_" + jointIndex;
                // Giải thích dòng gốc 144: Kiểm tra điều kiện thay thế khi các nhánh trước đó không đúng.
                else if (child.name.StartsWith("ArmBlock_"))
                    // Giải thích dòng gốc 145: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                    child.name = "ArmBlock_" + jointIndex;
            // Giải thích dòng gốc 146: Đóng khối code hiện tại.
            }
        // Giải thích dòng gốc 147: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 149: Khai báo hàm GetExistingJointCount với tham số trong ngoặc để thực hiện một hành vi.
        private int GetExistingJointCount()
        // Giải thích dòng gốc 150: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 151: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            int count = 0;
            // Giải thích dòng gốc 152: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
            for (int i = 0; i < _joints.Count; i++)
            // Giải thích dòng gốc 153: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
            {
                // Giải thích dòng gốc 154: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
                if (_joints[i] == null)
                    // Giải thích dòng gốc 155: Thoát khỏi vòng lặp hoặc switch hiện tại.
                    break;

                // Giải thích dòng gốc 157: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                count++;
            // Giải thích dòng gốc 158: Đóng khối code hiện tại.
            }

            // Giải thích dòng gốc 160: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return count;
        // Giải thích dòng gốc 161: Đóng khối code hiện tại.
        }
    // Giải thích dòng gốc 162: Đóng khối code hiện tại.
    }
// Giải thích dòng gốc 163: Đóng khối code hiện tại.
}
