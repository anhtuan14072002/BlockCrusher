// Giải thích dòng gốc 1: Nạp namespace UnityEngine để file dùng được các kiểu và API trong đó.
using UnityEngine;

// Giải thích dòng gốc 3: Khai báo namespace Crusher để gom nhóm code theo module.
namespace Crusher
// Giải thích dòng gốc 4: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
{
    // Giải thích dòng gốc 5: Khai báo class SuctionDevice để chứa dữ liệu và hành vi liên quan.
    public class SuctionDevice : MonoBehaviour
    // Giải thích dòng gốc 6: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 7: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        [SerializeField] private Transform _suctionPoint;
        // Giải thích dòng gốc 8: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        [SerializeField] private Vector3 _suctionBoxSize = new Vector3(4f, 2f, 1f);
        // Giải thích dòng gốc 9: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        [SerializeField] private float _suctionForce = 15f;
        // Giải thích dòng gốc 10: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        [SerializeField] private float _suctionAcceleration = 32f;
        // Giải thích dòng gốc 11: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        [SerializeField] private float _maxBlockVelocity = 8f;
        // Giải thích dòng gốc 12: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        [SerializeField] private float _arrivalDamping = 8f;
        // Giải thích dòng gốc 13: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        [SerializeField] private float _destroyRadius = 0.5f;
        // Giải thích dòng gốc 14: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        [SerializeField] private float _movementBlockRadius = 0.25f;
        // Giải thích dòng gốc 15: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        [SerializeField, Range(0.25f, 1f)] private float _slideProbeRadiusMultiplier = 0.55f;

        // Giải thích dòng gốc 17: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        private const float MovementSkin = 0.01f;
        // Giải thích dòng gốc 18: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        private const int SweepIterations = 6;
        // Giải thích dòng gốc 19: Khai báo hàm FixedUpdate với tham số trong ngoặc để thực hiện một hành vi.
        private void FixedUpdate()
        // Giải thích dòng gốc 20: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 21: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (!gameObject.activeInHierarchy) return;
            // Giải thích dòng gốc 22: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Transform refTransform = _suctionPoint != null ? _suctionPoint : transform;
            // Giải thích dòng gốc 23: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            TextureBlockSpawner.ApplySuctionForActiveSpawners(refTransform.position, refTransform.rotation, _suctionBoxSize,
                // Giải thích dòng gốc 24: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                _suctionForce, _suctionAcceleration, _maxBlockVelocity, _arrivalDamping, _destroyRadius,
                // Giải thích dòng gốc 25: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                Time.fixedDeltaTime);
        // Giải thích dòng gốc 26: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 28: Khai báo hàm ClampSawTarget với tham số trong ngoặc để thực hiện một hành vi.
        public Vector3 ClampSawTarget(Vector3 sawPosition, Vector3 targetSawPosition)
        // Giải thích dòng gốc 29: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 30: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 sawDelta = targetSawPosition - sawPosition;
            // Giải thích dòng gốc 31: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            sawDelta.z = 0f;
            // Giải thích dòng gốc 32: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (sawDelta.sqrMagnitude <= 0.000001f)
                // Giải thích dòng gốc 33: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return targetSawPosition;

            // Giải thích dòng gốc 35: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (TrySweepSawTarget(sawPosition, sawDelta, out Vector3 directTarget))
                // Giải thích dòng gốc 36: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return directTarget;

            // Giải thích dòng gốc 38: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 xDelta = new Vector3(sawDelta.x, 0f, 0f);
            // Giải thích dòng gốc 39: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 yDelta = new Vector3(0f, sawDelta.y, 0f);
            // Giải thích dòng gốc 40: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 xTarget = SweepSawTarget(sawPosition, xDelta);
            // Giải thích dòng gốc 41: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 yTarget = SweepSawTarget(sawPosition, yDelta);

            // Giải thích dòng gốc 43: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 xMove = xTarget - sawPosition;
            // Giải thích dòng gốc 44: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 yMove = yTarget - sawPosition;
            // Giải thích dòng gốc 45: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (xMove.sqrMagnitude <= 0.000001f && yMove.sqrMagnitude <= 0.000001f)
                // Giải thích dòng gốc 46: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return sawPosition;
            // Giải thích dòng gốc 47: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (xMove.sqrMagnitude <= 0.000001f)
                // Giải thích dòng gốc 48: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return yTarget;
            // Giải thích dòng gốc 49: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (yMove.sqrMagnitude <= 0.000001f)
                // Giải thích dòng gốc 50: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return xTarget;

            // Giải thích dòng gốc 52: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return Mathf.Abs(sawDelta.x) >= Mathf.Abs(sawDelta.y)
                // Giải thích dòng gốc 53: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                ? SweepSawTarget(xTarget, yMove)
                // Giải thích dòng gốc 54: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                : SweepSawTarget(yTarget, xMove);
        // Giải thích dòng gốc 55: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 57: Khai báo hàm IsBlockedByTexture với tham số trong ngoặc để thực hiện một hành vi.
        private bool IsBlockedByTexture(Vector3 suctionPosition, Vector3 directionToBlock, float blockDistance)
        // Giải thích dòng gốc 58: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 59: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float checkDistance = Mathf.Max(0f, blockDistance - _movementBlockRadius);
            // Giải thích dòng gốc 60: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            int steps = Mathf.Max(1, Mathf.CeilToInt(checkDistance / Mathf.Max(_movementBlockRadius * 0.5f, 0.05f)));

            // Giải thích dòng gốc 62: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
            for (int i = 1; i <= steps; i++)
            // Giải thích dòng gốc 63: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
            {
                // Giải thích dòng gốc 64: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                Vector3 samplePoint = suctionPosition + directionToBlock * (checkDistance * (i / (float)steps));
                // Giải thích dòng gốc 65: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
                if (TextureBlockSpawner.HasSolidAtWorldForActiveSpawners(samplePoint, _movementBlockRadius))
                    // Giải thích dòng gốc 66: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                    return true;
            // Giải thích dòng gốc 67: Đóng khối code hiện tại.
            }

            // Giải thích dòng gốc 69: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return false;
        // Giải thích dòng gốc 70: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 72: Khai báo hàm SweepSawTarget với tham số trong ngoặc để thực hiện một hành vi.
        private Vector3 SweepSawTarget(Vector3 sawPosition, Vector3 sawDelta)
        // Giải thích dòng gốc 73: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 74: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            TrySweepSawTarget(sawPosition, sawDelta, out Vector3 target);
            // Giải thích dòng gốc 75: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return target;
        // Giải thích dòng gốc 76: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 78: Khai báo hàm TrySweepSawTarget với tham số trong ngoặc để thực hiện một hành vi.
        private bool TrySweepSawTarget(Vector3 sawPosition, Vector3 sawDelta, out Vector3 targetSawPosition)
        // Giải thích dòng gốc 79: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 80: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            targetSawPosition = sawPosition;

            // Giải thích dòng gốc 82: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (sawDelta.sqrMagnitude <= 0.000001f)
                // Giải thích dòng gốc 83: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return true;

            // Giải thích dòng gốc 85: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 suctionStart = GetSuctionPosition();
            // Giải thích dòng gốc 86: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (!IsSuctionPathBlocked(suctionStart, sawDelta, 1f))
            // Giải thích dòng gốc 87: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
            {
                // Giải thích dòng gốc 88: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                targetSawPosition = sawPosition + sawDelta;
                // Giải thích dòng gốc 89: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return true;
            // Giải thích dòng gốc 90: Đóng khối code hiện tại.
            }

            // Giải thích dòng gốc 92: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float low = 0f;
            // Giải thích dòng gốc 93: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float high = 1f;
            // Giải thích dòng gốc 94: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
            for (int i = 0; i < SweepIterations; i++)
            // Giải thích dòng gốc 95: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
            {
                // Giải thích dòng gốc 96: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                float middle = (low + high) * 0.5f;
                // Giải thích dòng gốc 97: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
                if (IsSuctionPathBlocked(suctionStart, sawDelta, middle))
                    // Giải thích dòng gốc 98: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                    high = middle;
                // Giải thích dòng gốc 99: Chạy nhánh thay thế khi điều kiện if trước đó không đúng.
                else
                    // Giải thích dòng gốc 100: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                    low = middle;
            // Giải thích dòng gốc 101: Đóng khối code hiện tại.
            }

            // Giải thích dòng gốc 103: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 safeDelta = sawDelta * low;
            // Giải thích dòng gốc 104: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float safeDistance = safeDelta.magnitude;
            // Giải thích dòng gốc 105: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (safeDistance > MovementSkin)
                // Giải thích dòng gốc 106: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                safeDelta -= safeDelta / safeDistance * MovementSkin;
            // Giải thích dòng gốc 107: Chạy nhánh thay thế khi điều kiện if trước đó không đúng.
            else
                // Giải thích dòng gốc 108: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                safeDelta = Vector3.zero;

            // Giải thích dòng gốc 110: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            targetSawPosition = sawPosition + safeDelta;
            // Giải thích dòng gốc 111: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return false;
        // Giải thích dòng gốc 112: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 114: Khai báo hàm IsSuctionPathBlocked với tham số trong ngoặc để thực hiện một hành vi.
        private bool IsSuctionPathBlocked(Vector3 suctionStart, Vector3 sawDelta, float normalizedDistance)
        // Giải thích dòng gốc 115: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 116: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 delta = sawDelta * normalizedDistance;
            // Giải thích dòng gốc 117: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            float distance = delta.magnitude;
            // Giải thích dòng gốc 118: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (distance <= 0.0001f)
                // Giải thích dòng gốc 119: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return false;

            // Giải thích dòng gốc 121: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(_movementBlockRadius * 0.5f, 0.05f)));
            // Giải thích dòng gốc 122: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
            for (int i = 1; i <= steps; i++)
            // Giải thích dòng gốc 123: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
            {
                // Giải thích dòng gốc 124: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
                Vector3 samplePoint = suctionStart + delta * (i / (float)steps);
                // Giải thích dòng gốc 125: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
                if (TextureBlockSpawner.HasSolidAtWorldForActiveSpawners(samplePoint, GetSlideProbeRadius()))
                    // Giải thích dòng gốc 126: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                    return true;
            // Giải thích dòng gốc 127: Đóng khối code hiện tại.
            }

            // Giải thích dòng gốc 129: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return false;
        // Giải thích dòng gốc 130: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 132: Khai báo hàm GetSuctionPosition với tham số trong ngoặc để thực hiện một hành vi.
        private Vector3 GetSuctionPosition()
        // Giải thích dòng gốc 133: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 134: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return _suctionPoint != null ? _suctionPoint.position : transform.position;
        // Giải thích dòng gốc 135: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 137: Khai báo hàm GetSlideProbeRadius với tham số trong ngoặc để thực hiện một hành vi.
        private float GetSlideProbeRadius()
        // Giải thích dòng gốc 138: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 139: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return _movementBlockRadius * _slideProbeRadiusMultiplier;
        // Giải thích dòng gốc 140: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 142: Khai báo hàm OnDrawGizmosSelected với tham số trong ngoặc để thực hiện một hành vi.
        private void OnDrawGizmosSelected()
        // Giải thích dòng gốc 143: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 144: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Transform refTransform = _suctionPoint != null ? _suctionPoint : transform;

            // Giải thích dòng gốc 146: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 boxCenter = refTransform.position + refTransform.right * (_suctionBoxSize.x / 2f);

            // Giải thích dòng gốc 148: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            // Giải thích dòng gốc 149: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Gizmos.matrix = Matrix4x4.TRS(boxCenter, refTransform.rotation, Vector3.one);
            // Giải thích dòng gốc 150: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            Gizmos.DrawCube(Vector3.zero, _suctionBoxSize);
            // Giải thích dòng gốc 151: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Gizmos.color = Color.green;
            // Giải thích dòng gốc 152: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            Gizmos.DrawWireCube(Vector3.zero, _suctionBoxSize);
        // Giải thích dòng gốc 153: Đóng khối code hiện tại.
        }
    // Giải thích dòng gốc 154: Đóng khối code hiện tại.
    }
// Giải thích dòng gốc 155: Đóng khối code hiện tại.
}
