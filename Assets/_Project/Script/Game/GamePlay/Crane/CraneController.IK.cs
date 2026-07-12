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
        // Giải thích dòng gốc 7: Khai báo hàm SolveJointsToSaw với tham số trong ngoặc để thực hiện một hành vi.
        private void SolveJointsToSaw()
        // Giải thích dòng gốc 8: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 9: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (_saw == null || _activeJointCount == 0 || _solvePositions == null)
                // Giải thích dòng gốc 10: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return;

            // Giải thích dòng gốc 12: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            int segmentCount = _activeJointCount;
            // Giải thích dòng gốc 13: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 rootPosition = GetRootPosition();
            // Giải thích dòng gốc 14: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 targetPosition = _sawTarget;
            // Giải thích dòng gốc 15: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 rootToTarget = targetPosition - rootPosition;
            // Giải thích dòng gốc 16: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float maxReach = GetActiveReach();

            // Giải thích dòng gốc 18: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
            for (int i = 0; i < _activeJointCount; i++)
                // Giải thích dòng gốc 19: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                _solvePositions[i] = _joints[i] != null ? _joints[i].position : rootPosition;

            // Giải thích dòng gốc 21: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (rootToTarget.sqrMagnitude >= maxReach * maxReach)
            // Giải thích dòng gốc 22: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
            {
                // Giải thích dòng gốc 23: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                Vector3 direction = rootToTarget.normalized;
                // Giải thích dòng gốc 24: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                _solvePositions[0] = rootPosition;

                // Giải thích dòng gốc 26: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
                for (int i = 1; i <= segmentCount; i++)
                    // Giải thích dòng gốc 27: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                    _solvePositions[i] = _solvePositions[i - 1] + direction * _segmentLengths[i - 1];

                // Giải thích dòng gốc 29: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                _sawTarget = _solvePositions[segmentCount];
                // Giải thích dòng gốc 30: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                ApplySolvedJoints();
                // Giải thích dòng gốc 31: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return;
            // Giải thích dòng gốc 32: Đóng khối code hiện tại.
            }

            // Giải thích dòng gốc 34: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _solvePositions[0] = rootPosition;
            // Giải thích dòng gốc 35: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _solvePositions[segmentCount] = targetPosition;

            // Giải thích dòng gốc 37: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
            for (int iteration = 0; iteration < _ikIterations; iteration++)
            // Giải thích dòng gốc 38: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
            {
                // Giải thích dòng gốc 39: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
                for (int i = segmentCount - 1; i >= 0; i--)
                // Giải thích dòng gốc 40: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
                {
                    // Giải thích dòng gốc 41: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                    Vector3 direction = (_solvePositions[i] - _solvePositions[i + 1]).normalized;
                    // Giải thích dòng gốc 42: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                    _solvePositions[i] = _solvePositions[i + 1] + direction * _segmentLengths[i];
                // Giải thích dòng gốc 43: Đóng khối code hiện tại.
                }

                // Giải thích dòng gốc 45: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                _solvePositions[0] = rootPosition;

                // Giải thích dòng gốc 47: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
                for (int i = 1; i <= segmentCount; i++)
                // Giải thích dòng gốc 48: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
                {
                    // Giải thích dòng gốc 49: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                    Vector3 direction = (_solvePositions[i] - _solvePositions[i - 1]).normalized;
                    // Giải thích dòng gốc 50: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                    _solvePositions[i] = _solvePositions[i - 1] + direction * _segmentLengths[i - 1];
                // Giải thích dòng gốc 51: Đóng khối code hiện tại.
                }

                // Giải thích dòng gốc 53: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                _solvePositions[segmentCount] = targetPosition;
            // Giải thích dòng gốc 54: Đóng khối code hiện tại.
            }

            // Giải thích dòng gốc 56: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            ApplySolvedJoints();
        // Giải thích dòng gốc 57: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 59: Khai báo hàm ApplySolvedJoints với tham số trong ngoặc để thực hiện một hành vi.
        private void ApplySolvedJoints()
        // Giải thích dòng gốc 60: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 61: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
            for (int i = 0; i < _activeJointCount; i++)
            // Giải thích dòng gốc 62: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
            {
                // Giải thích dòng gốc 63: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                Transform joint = _joints[i];
                // Giải thích dòng gốc 64: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
                if (joint == null)
                    // Giải thích dòng gốc 65: Bỏ qua phần còn lại của lần lặp hiện tại và sang lần tiếp theo.
                    continue;

                // Giải thích dòng gốc 67: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                Vector3 direction = _solvePositions[i + 1] - _solvePositions[i];
                // Giải thích dòng gốc 68: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                float length = direction.magnitude;
                // Giải thích dòng gốc 69: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
                if (length <= 0.0001f)
                    // Giải thích dòng gốc 70: Bỏ qua phần còn lại của lần lặp hiện tại và sang lần tiếp theo.
                    continue;

                // Giải thích dòng gốc 72: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                joint.position = _solvePositions[i];
                // Giải thích dòng gốc 73: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                joint.rotation = GetSegmentRotation(direction) * _jointRotationOffsets[i];
            // Giải thích dòng gốc 74: Đóng khối code hiện tại.
            }

            // Giải thích dòng gốc 76: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 sawDirection = _solvePositions[_activeJointCount] - _solvePositions[_activeJointCount - 1];
            // Giải thích dòng gốc 77: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _saw.position = _solvePositions[_activeJointCount];

            // Giải thích dòng gốc 79: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (_useSawInputRotation && _lastSawMoveDirection.sqrMagnitude > 0.0001f)
                // Giải thích dòng gốc 80: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                _saw.rotation = GetSegmentRotation(_lastSawMoveDirection) * _sawRotationOffset;
            // Giải thích dòng gốc 81: Kiểm tra điều kiện thay thế khi các nhánh trước đó không đúng.
            else if (sawDirection.sqrMagnitude > 0.0001f)
                // Giải thích dòng gốc 82: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                _saw.rotation = GetSegmentRotation(sawDirection) * _sawRotationOffset;
        // Giải thích dòng gốc 83: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 85: Khai báo hàm SeedZigZagPoseToSaw với tham số trong ngoặc để thực hiện một hành vi.
        private void SeedZigZagPoseToSaw()
        // Giải thích dòng gốc 86: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 87: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (_saw == null || _activeJointCount <= 1)
                // Giải thích dòng gốc 88: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return;

            // Giải thích dòng gốc 90: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 rootPosition = GetRootPosition();
            // Giải thích dòng gốc 91: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 sawPosition = _saw.position;
            // Giải thích dòng gốc 92: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float reach = GetActiveReach();
            // Giải thích dòng gốc 93: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float targetDistance = Vector3.Distance(rootPosition, sawPosition);
            // Giải thích dòng gốc 94: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (targetDistance <= 0.0001f)
                // Giải thích dòng gốc 95: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return;

            // Giải thích dòng gốc 97: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            int segmentCount = _activeJointCount;
            // Giải thích dòng gốc 98: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 forward = (sawPosition - rootPosition) / targetDistance;
            // Giải thích dòng gốc 99: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 bendNormal = GetPreferredZigZagNormal(rootPosition, forward);
            // Giải thích dòng gốc 100: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float slack = Mathf.Max(0f, reach - targetDistance);
            // Giải thích dòng gốc 101: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float amplitude = Mathf.Min(_segmentLength * 0.55f, slack * 0.5f);
            // Giải thích dòng gốc 102: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (amplitude <= 0.0001f)
                // Giải thích dòng gốc 103: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                amplitude = _segmentLength * 0.25f;

            // Giải thích dòng gốc 105: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _solvePositions[0] = rootPosition;
            // Giải thích dòng gốc 106: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
            for (int i = 1; i < segmentCount; i++)
            // Giải thích dòng gốc 107: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
            {
                // Giải thích dòng gốc 108: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                float t = i / (float)segmentCount;
                // Giải thích dòng gốc 109: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                float sign = (i & 1) == 0 ? -1f : 1f;
                // Giải thích dòng gốc 110: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                _solvePositions[i] = Vector3.Lerp(rootPosition, sawPosition, t) + bendNormal * (amplitude * sign);
            // Giải thích dòng gốc 111: Đóng khối code hiện tại.
            }

            // Giải thích dòng gốc 113: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _solvePositions[segmentCount] = sawPosition;
            // Giải thích dòng gốc 114: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            ApplySolvedJoints();
        // Giải thích dòng gốc 115: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 117: Khai báo hàm GetPreferredZigZagNormal với tham số trong ngoặc để thực hiện một hành vi.
        private Vector3 GetPreferredZigZagNormal(Vector3 rootPosition, Vector3 forward)
        // Giải thích dòng gốc 118: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 119: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 normal = new Vector3(-forward.y, forward.x, 0f);
            // Giải thích dòng gốc 120: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float side = 0f;
            // Giải thích dòng gốc 121: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
            for (int i = 1; i < _activeJointCount; i++)
            // Giải thích dòng gốc 122: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
            {
                // Giải thích dòng gốc 123: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                Transform joint = _joints[i];
                // Giải thích dòng gốc 124: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
                if (joint != null)
                    // Giải thích dòng gốc 125: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                    side += Vector3.Dot(joint.position - rootPosition, normal);
            // Giải thích dòng gốc 126: Đóng khối code hiện tại.
            }

            // Giải thích dòng gốc 128: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (Mathf.Abs(side) > 0.0001f)
                // Giải thích dòng gốc 129: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return normal * Mathf.Sign(side);

            // Giải thích dòng gốc 131: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return normal;
        // Giải thích dòng gốc 132: Đóng khối code hiện tại.
        }
    // Giải thích dòng gốc 133: Đóng khối code hiện tại.
    }
// Giải thích dòng gốc 134: Đóng khối code hiện tại.
}
