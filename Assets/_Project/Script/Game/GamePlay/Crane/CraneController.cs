// Giải thích dòng gốc 1: Nạp namespace System.Collections.Generic để file dùng được các kiểu và API trong đó.
using System.Collections.Generic;
// Giải thích dòng gốc 2: Nạp namespace UnityEngine để file dùng được các kiểu và API trong đó.
using UnityEngine;
// Giải thích dòng gốc 3: Nạp namespace UnityEngine.UI để file dùng được các kiểu và API trong đó.
using UnityEngine.UI;

// Giải thích dòng gốc 5: Khai báo namespace Crusher để gom nhóm code theo module.
namespace Crusher
// Giải thích dòng gốc 6: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
{
    // Giải thích dòng gốc 7: Khai báo partial class CraneController, cho phép chia class này qua nhiều file.
    public sealed partial class CraneController : MonoBehaviour
    // Giải thích dòng gốc 8: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 9: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        [SerializeField] private Joystick _joystick;
        // Giải thích dòng gốc 10: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        [SerializeField] private Button _addJointButton;

        // Giải thích dòng gốc 12: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        [Space] [SerializeField] private float _sawMoveSpeed = 3.5f;
        // Giải thích dòng gốc 13: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        [SerializeField, Range(0.05f, 1f)] private float _sawContactMoveMultiplier = 0.35f;
        // Giải thích dòng gốc 14: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        [SerializeField] private Vector2 _targetXBounds = new Vector2(-3.8f, 3.8f);
        // Giải thích dòng gốc 15: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        [SerializeField] private Vector2 _targetYBounds = new Vector2(-2.6f, 6.2f);
        // Giải thích dòng gốc 16: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        [SerializeField] private int _ikIterations = 16;
        // Giải thích dòng gốc 17: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        [SerializeField] private Camera _movementCamera;

        // Giải thích dòng gốc 19: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        [Space] [SerializeField] private int _activeJointCount = 4;
        // Giải thích dòng gốc 20: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        [SerializeField] private int _maxJointCount = 7;
        // Giải thích dòng gốc 21: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        [SerializeField] private float _segmentLength = 1.15f;
        // Giải thích dòng gốc 22: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        [SerializeField] private GameObject _jointPrefab;
        // Giải thích dòng gốc 23: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        [SerializeField] private List<Transform> _joints = new List<Transform>(8);
        // Giải thích dòng gốc 24: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        [SerializeField] private Transform _saw;
        // Giải thích dòng gốc 25: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        [SerializeField] private SawBlockCutter _sawCutter;
        // Giải thích dòng gốc 26: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        [SerializeField] private SuctionDevice _suctionDevice;
        // Giải thích dòng gốc 27: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        [SerializeField] private Button _switchToolButton;

        // Giải thích dòng gốc 29: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        private Vector3[] _solvePositions;
        // Giải thích dòng gốc 30: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        private float[] _segmentLengths;
        // Giải thích dòng gốc 31: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        private Quaternion[] _jointRotationOffsets;
        // Giải thích dòng gốc 32: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        private Quaternion _sawRotationOffset = Quaternion.identity;
        // Giải thích dòng gốc 33: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        private Transform _movementCameraTransform;
        // Giải thích dòng gốc 34: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        private Transform _rootParent;
        // Giải thích dòng gốc 35: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        private Vector3 _rootLocalPosition;                                        
        // Giải thích dòng gốc 36: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        private Vector3 _sawTarget;
        // Giải thích dòng gốc 37: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        private Vector3 _lastSawMoveDirection;
        // Giải thích dòng gốc 38: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        private float _activeReach;
        // Giải thích dòng gốc 39: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
        private bool _isSuctionMode;
        // Giải thích dòng gốc 40: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        private bool _useSawInputRotation = true;
        
        // Giải thích dòng gốc 42: Khai báo hàm Awake với tham số trong ngoặc để thực hiện một hành vi.
        private void Awake()
        // Giải thích dòng gốc 43: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 44: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            Application.targetFrameRate = 60;
            // Giải thích dòng gốc 45: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (_joystick == null)
            // Giải thích dòng gốc 46: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
            {
                // Giải thích dòng gốc 47: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
                _joystick = FindFirstObjectByType<Joystick>();
                // Giải thích dòng gốc 48: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                _joystick.gameObject.SetActive(true);
            // Giải thích dòng gốc 49: Đóng khối code hiện tại.
            }
            // Giải thích dòng gốc 50: Chạy nhánh thay thế khi điều kiện if trước đó không đúng.
            else
            // Giải thích dòng gốc 51: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
            {
                // Giải thích dòng gốc 52: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                _joystick.gameObject.SetActive(true);
            // Giải thích dòng gốc 53: Đóng khối code hiện tại.
            }

            // Giải thích dòng gốc 55: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            CacheMovementCamera();

            // Giải thích dòng gốc 57: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (_switchToolButton != null)
            // Giải thích dòng gốc 58: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
            {
                // Giải thích dòng gốc 59: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
                _switchToolButton.onClick.AddListener(ToggleTool);
            // Giải thích dòng gốc 60: Đóng khối code hiện tại.
            }

            // Giải thích dòng gốc 62: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            int existingJointCount = GetExistingJointCount();
            // Giải thích dòng gốc 63: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _maxJointCount = Mathf.Max(_maxJointCount, existingJointCount);
            // Giải thích dòng gốc 64: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _activeJointCount = existingJointCount > 0 ? Mathf.Clamp(_activeJointCount, 1, existingJointCount) : 0;

            // Giải thích dòng gốc 66: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            AllocateSolverBuffers();
            // Giải thích dòng gốc 67: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            CacheFixedRoot();
            // Giải thích dòng gốc 68: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            CacheModelPoseOffsets();
            // Giải thích dòng gốc 69: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            DetachSawFromJoints();
            // Giải thích dòng gốc 70: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            ApplyActiveJointCount();
            // Giải thích dòng gốc 71: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            CacheSawCutter();

            // Giải thích dòng gốc 73: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _sawTarget = GetSawPosition();
            
            // Giải thích dòng gốc 75: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            SetToolActive(false);
        // Giải thích dòng gốc 76: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 78: Khai báo hàm Update với tham số trong ngoặc để thực hiện một hành vi.
        private void Update()
        // Giải thích dòng gốc 79: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 80: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector2 input = _joystick != null ? _joystick.Direction : Vector2.zero;
            // Giải thích dòng gốc 81: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (input.sqrMagnitude <= 0.0001f)
                // Giải thích dòng gốc 82: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return;

            // Giải thích dòng gốc 84: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            MoveSawTarget(input);
            // Giải thích dòng gốc 85: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            SolveJointsToSaw();
        // Giải thích dòng gốc 86: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 88: Khai báo hàm AddJoint với tham số trong ngoặc để thực hiện một hành vi.
        public void AddJoint()
        // Giải thích dòng gốc 89: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 90: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (_activeJointCount >= _maxJointCount)
                // Giải thích dòng gốc 91: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return;

            // Giải thích dòng gốc 93: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 lockedSawPosition = GetSawPosition();
            // Giải thích dòng gốc 94: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            int insertIndex = _activeJointCount - 1;
            // Giải thích dòng gốc 95: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            int storageIndex = _activeJointCount;
            // Giải thích dòng gốc 96: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Transform insertedJoint = GetOrCreateJoint(storageIndex);
            // Giải thích dòng gốc 97: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (insertedJoint == null)
                // Giải thích dòng gốc 98: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
                return;

            // Giải thích dòng gốc 100: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _sawTarget = lockedSawPosition;
            // Giải thích dòng gốc 101: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            InsertJointBeforeLast(insertIndex, storageIndex, insertedJoint);
            // Giải thích dòng gốc 102: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
            _activeJointCount++;
            // Giải thích dòng gốc 103: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            ApplyActiveJointCount();
            // Giải thích dòng gốc 104: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _useSawInputRotation = false;
            // Giải thích dòng gốc 105: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            SeedZigZagPoseToSaw();
            // Giải thích dòng gốc 106: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            SolveJointsToSaw();
            // Giải thích dòng gốc 107: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            CacheSawRotationOffset();
            // Giải thích dòng gốc 108: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            ApplySawAtPosition(lockedSawPosition);
            // Giải thích dòng gốc 109: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _useSawInputRotation = true;
        // Giải thích dòng gốc 110: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 112: Khai báo hàm ToggleTool với tham số trong ngoặc để thực hiện một hành vi.
        public void ToggleTool()
        // Giải thích dòng gốc 113: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 114: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _isSuctionMode = !_isSuctionMode;
            // Giải thích dòng gốc 115: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            SetToolActive(_isSuctionMode);
        // Giải thích dòng gốc 116: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 118: Khai báo hàm SetToolActive với tham số trong ngoặc để thực hiện một hành vi.
        private void SetToolActive(bool isSuction)
        // Giải thích dòng gốc 119: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 120: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (_sawCutter != null) _sawCutter.gameObject.SetActive(!isSuction);
            // Giải thích dòng gốc 121: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
            if (_suctionDevice != null) _suctionDevice.gameObject.SetActive(isSuction);
        // Giải thích dòng gốc 122: Đóng khối code hiện tại.
        }
    // Giải thích dòng gốc 123: Đóng khối code hiện tại.
    }
// Giải thích dòng gốc 124: Đóng khối code hiện tại.
}
