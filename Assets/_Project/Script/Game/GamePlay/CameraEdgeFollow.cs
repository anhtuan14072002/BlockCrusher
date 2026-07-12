// Giải thích dòng gốc 1: Nạp namespace UnityEngine để file dùng được các kiểu và API trong đó.
using UnityEngine;

// Giải thích dòng gốc 3: Khai báo class CameraEdgeFollow để chứa dữ liệu và hành vi liên quan.
public sealed class CameraEdgeFollow : MonoBehaviour
// Giải thích dòng gốc 4: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
{
    // Giải thích dòng gốc 5: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    [SerializeField] private Transform _target;
    // Giải thích dòng gốc 6: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    [SerializeField] private Camera _camera;
    // Giải thích dòng gốc 7: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField] private Vector2 _minViewport = new Vector2(0.28f, 0.24f);
    // Giải thích dòng gốc 8: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField] private Vector2 _maxViewport = new Vector2(0.72f, 0.76f);
    // Giải thích dòng gốc 9: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField] private float _smoothTime = 0.12f;

    // Giải thích dòng gốc 11: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private Vector3 _velocity;
    // Giải thích dòng gốc 12: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private Transform _transform;
    // Giải thích dòng gốc 13: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private float _lastOrthographicSize;
    // Giải thích dòng gốc 14: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private float _lastAspect;
    // Giải thích dòng gốc 15: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private float _worldHeight;
    // Giải thích dòng gốc 16: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private float _worldWidth;

    // Giải thích dòng gốc 18: Khai báo hàm Awake với tham số trong ngoặc để thực hiện một hành vi.
    private void Awake()
    // Giải thích dòng gốc 19: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 20: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _transform = transform;
        // Giải thích dòng gốc 21: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_camera == null)
            // Giải thích dòng gốc 22: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _camera = GetComponent<Camera>();

        // Giải thích dòng gốc 24: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        RefreshCameraSize();
    // Giải thích dòng gốc 25: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 27: Khai báo hàm LateUpdate với tham số trong ngoặc để thực hiện một hành vi.
    private void LateUpdate()
    // Giải thích dòng gốc 28: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 29: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_target == null || _camera == null)
            // Giải thích dòng gốc 30: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return;

        // Giải thích dòng gốc 32: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        RefreshCameraSize();
        // Giải thích dòng gốc 33: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        Vector3 targetPosition = GetCameraTargetPosition();
        // Giải thích dòng gốc 34: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        Vector3 cameraPosition = _transform.position;
        // Giải thích dòng gốc 35: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if ((targetPosition - cameraPosition).sqrMagnitude <= 0.000001f)
            // Giải thích dòng gốc 36: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return;

        // Giải thích dòng gốc 38: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _transform.position = Vector3.SmoothDamp(cameraPosition, targetPosition, ref _velocity, _smoothTime);
    // Giải thích dòng gốc 39: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 41: Khai báo hàm GetCameraTargetPosition với tham số trong ngoặc để thực hiện một hành vi.
    private Vector3 GetCameraTargetPosition()
    // Giải thích dòng gốc 42: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 43: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        Vector3 viewportPosition = _camera.WorldToViewportPoint(_target.position);
        // Giải thích dòng gốc 44: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        Vector3 cameraPosition = _transform.position;

        // Giải thích dòng gốc 46: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (viewportPosition.x < _minViewport.x)
            // Giải thích dòng gốc 47: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            cameraPosition.x += (viewportPosition.x - _minViewport.x) * _worldWidth;
        // Giải thích dòng gốc 48: Kiểm tra điều kiện thay thế khi các nhánh trước đó không đúng.
        else if (viewportPosition.x > _maxViewport.x)
            // Giải thích dòng gốc 49: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            cameraPosition.x += (viewportPosition.x - _maxViewport.x) * _worldWidth;

        // Giải thích dòng gốc 51: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (viewportPosition.y < _minViewport.y)
            // Giải thích dòng gốc 52: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            cameraPosition.y += (viewportPosition.y - _minViewport.y) * _worldHeight;
        // Giải thích dòng gốc 53: Kiểm tra điều kiện thay thế khi các nhánh trước đó không đúng.
        else if (viewportPosition.y > _maxViewport.y)
            // Giải thích dòng gốc 54: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            cameraPosition.y += (viewportPosition.y - _maxViewport.y) * _worldHeight;

        // Giải thích dòng gốc 56: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
        return cameraPosition;
    // Giải thích dòng gốc 57: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 59: Khai báo hàm RefreshCameraSize với tham số trong ngoặc để thực hiện một hành vi.
    private void RefreshCameraSize()
    // Giải thích dòng gốc 60: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 61: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_camera == null && _lastOrthographicSize > 0f)
            // Giải thích dòng gốc 62: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return;

        // Giải thích dòng gốc 64: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        float orthographicSize = _camera != null ? _camera.orthographicSize : 0f;
        // Giải thích dòng gốc 65: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        float aspect = _camera != null ? _camera.aspect : 0f;
        // Giải thích dòng gốc 66: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (orthographicSize == _lastOrthographicSize && aspect == _lastAspect)
            // Giải thích dòng gốc 67: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return;

        // Giải thích dòng gốc 69: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _lastOrthographicSize = orthographicSize;
        // Giải thích dòng gốc 70: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _lastAspect = aspect;
        // Giải thích dòng gốc 71: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _worldHeight = orthographicSize * 2f;
        // Giải thích dòng gốc 72: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _worldWidth = _worldHeight * aspect;
    // Giải thích dòng gốc 73: Đóng khối code hiện tại.
    }
// Giải thích dòng gốc 74: Đóng khối code hiện tại.
}
