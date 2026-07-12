// Giải thích dòng gốc 1: Nạp namespace System.Collections.Generic để file dùng được các kiểu và API trong đó.
using System.Collections.Generic;
// Giải thích dòng gốc 2: Nạp namespace UnityEngine để file dùng được các kiểu và API trong đó.
using UnityEngine;

// Giải thích dòng gốc 4: Khai báo class SawBlockCutter để chứa dữ liệu và hành vi liên quan.
public sealed class SawBlockCutter : MonoBehaviour
// Giải thích dòng gốc 5: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
{
    // Giải thích dòng gốc 6: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField] private float _compressionForce = 9f;
    // Giải thích dòng gốc 7: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField] private float _bladeTangentialForce = 32f;
    // Giải thích dòng gốc 8: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField] private float _bladeSpinDirection = 1f;
    // Giải thích dòng gốc 9: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField] private float _bladeSpinSpeed = 900f;
    // Giải thích dòng gốc 10: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField] private float _bladePushRadius = 0.55f;
    // Giải thích dòng gốc 11: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField] private float _maxSawVelocity = 8f;
    // Giải thích dòng gốc 12: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField] private float _cutSweepStep = 0.08f;
    // Giải thích dòng gốc 13: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField, Range(0f, 1f)] private float _sideDampingOnContact = 0.35f;
    // Giải thích dòng gốc 14: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField] private float _maxBlockVelocity = 11f;
    // Giải thích dòng gốc 15: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    [SerializeField] private float _resistanceDuration = 0.08f;
    // Giải thích dòng gốc 16: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    [SerializeField] private Transform _bladeVisual;

    // Giải thích dòng gốc 18: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private Vector3 _previousPosition;
    // Giải thích dòng gốc 19: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private Vector3 _sawVelocity;
    // Giải thích dòng gốc 20: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private Vector3 _pressDirection;
    // Giải thích dòng gốc 21: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private float _pressSpeed;
    // Giải thích dòng gốc 22: Khai báo trường hoặc thuộc tính thành viên của kiểu hiện tại.
    private float _resistanceUntil;
    // Giải thích dòng gốc 23: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    private readonly Dictionary<Collider, TextureBlockChunk> _chunkCache = new Dictionary<Collider, TextureBlockChunk>(16);

    // Giải thích dòng gốc 25: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
    public bool IsResisting => Time.time < _resistanceUntil;

    // Giải thích dòng gốc 27: Khai báo hàm Awake với tham số trong ngoặc để thực hiện một hành vi.
    private void Awake()
    // Giải thích dòng gốc 28: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 29: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _bladeVisual ??= transform;
        // Giải thích dòng gốc 30: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _previousPosition = transform.position;
    // Giải thích dòng gốc 31: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 33: Khai báo hàm Update với tham số trong ngoặc để thực hiện một hành vi.
    private void Update()
    // Giải thích dòng gốc 34: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 35: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_bladeVisual == null || _bladeSpinSpeed == 0f)
            // Giải thích dòng gốc 36: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return;

        // Giải thích dòng gốc 38: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        _bladeVisual.Rotate(0f, 0f, _bladeSpinSpeed * Mathf.Sign(_bladeSpinDirection) * Time.deltaTime, Space.Self);
    // Giải thích dòng gốc 39: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 41: Khai báo hàm FixedUpdate với tham số trong ngoặc để thực hiện một hành vi.
    private void FixedUpdate()
    // Giải thích dòng gốc 42: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 43: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        Vector3 previousPosition = _previousPosition;
        // Giải thích dòng gốc 44: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        Vector3 currentPosition = transform.position;
        // Giải thích dòng gốc 45: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        float inverseDeltaTime = Time.fixedDeltaTime > 0f ? 1f / Time.fixedDeltaTime : 0f;
        // Giải thích dòng gốc 46: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _sawVelocity = (currentPosition - previousPosition) * inverseDeltaTime;

        // Giải thích dòng gốc 48: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_sawVelocity.sqrMagnitude > _maxSawVelocity * _maxSawVelocity)
            // Giải thích dòng gốc 49: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            _sawVelocity = _sawVelocity.normalized * _maxSawVelocity;

        // Giải thích dòng gốc 51: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        Vector3 planarVelocity = new Vector3(_sawVelocity.x, _sawVelocity.y, 0f);
        // Giải thích dòng gốc 52: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _pressSpeed = planarVelocity.magnitude;
        // Giải thích dòng gốc 53: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _pressDirection = _pressSpeed > 0.0001f ? planarVelocity / _pressSpeed : Vector3.zero;

        // Giải thích dòng gốc 55: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _previousPosition = currentPosition;

        // Giải thích dòng gốc 57: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (ReleaseAlongMovement(previousPosition, currentPosition))
            // Giải thích dòng gốc 58: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            RegisterResistance();
    // Giải thích dòng gốc 59: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 61: Khai báo hàm OnTriggerEnter với tham số trong ngoặc để thực hiện một hành vi.
    private void OnTriggerEnter(Collider other)
    // Giải thích dòng gốc 62: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 63: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        ReleaseAndPush(other);
    // Giải thích dòng gốc 64: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 66: Khai báo hàm OnTriggerStay với tham số trong ngoặc để thực hiện một hành vi.
    private void OnTriggerStay(Collider other)
    // Giải thích dòng gốc 67: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 68: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        ReleaseAndPush(other);
    // Giải thích dòng gốc 69: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 71: Khai báo hàm OnCollisionEnter với tham số trong ngoặc để thực hiện một hành vi.
    private void OnCollisionEnter(Collision collision)
    // Giải thích dòng gốc 72: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 73: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        ReleaseAndPush(collision.collider, collision.GetContact(0).point);
    // Giải thích dòng gốc 74: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 76: Khai báo hàm OnCollisionStay với tham số trong ngoặc để thực hiện một hành vi.
    private void OnCollisionStay(Collision collision)
    // Giải thích dòng gốc 77: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 78: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        ReleaseAndPush(collision.collider, collision.GetContact(0).point);
    // Giải thích dòng gốc 79: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 81: Khai báo hàm OnCollisionExit với tham số trong ngoặc để thực hiện một hành vi.
    private void OnCollisionExit(Collision collision)
    // Giải thích dòng gốc 82: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 83: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        ClearCache(collision.collider);
    // Giải thích dòng gốc 84: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 86: Khai báo hàm OnTriggerExit với tham số trong ngoặc để thực hiện một hành vi.
    private void OnTriggerExit(Collider other)
    // Giải thích dòng gốc 87: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 88: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        ClearCache(other);
    // Giải thích dòng gốc 89: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 91: Khai báo hàm OnDisable với tham số trong ngoặc để thực hiện một hành vi.
    private void OnDisable()
    // Giải thích dòng gốc 92: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 93: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        _chunkCache.Clear();
    // Giải thích dòng gốc 94: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 96: Khai báo hàm ReleaseAndPush với tham số trong ngoặc để thực hiện một hành vi.
    private void ReleaseAndPush(Collider other)
    // Giải thích dòng gốc 97: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 98: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        Vector3 contactPoint = other.ClosestPoint(transform.position);
        // Giải thích dòng gốc 99: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        ReleaseAndPush(other, contactPoint);
    // Giải thích dòng gốc 100: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 102: Khai báo hàm ReleaseAndPush với tham số trong ngoặc để thực hiện một hành vi.
    private void ReleaseAndPush(Collider other, Vector3 contactPoint)
    // Giải thích dòng gốc 103: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 104: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        TextureBlockChunk chunk = GetChunk(other);
        // Giải thích dòng gốc 105: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (chunk != null && chunk.ReleaseAtWorld(contactPoint, _pressDirection, _pressSpeed, _compressionForce,
                // Giải thích dòng gốc 106: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                _bladeTangentialForce, _bladeSpinDirection, _bladePushRadius, _sideDampingOnContact, _maxBlockVelocity))
            // Giải thích dòng gốc 107: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
            RegisterResistance();
    // Giải thích dòng gốc 108: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 110: Khai báo hàm RegisterResistance với tham số trong ngoặc để thực hiện một hành vi.
    private void RegisterResistance()
    // Giải thích dòng gốc 111: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 112: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        _resistanceUntil = Time.time + _resistanceDuration;
    // Giải thích dòng gốc 113: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 115: Khai báo hàm ReleaseAlongMovement với tham số trong ngoặc để thực hiện một hành vi.
    private bool ReleaseAlongMovement(Vector3 from, Vector3 to)
    // Giải thích dòng gốc 116: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 117: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        Vector3 delta = to - from;
        // Giải thích dòng gốc 118: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        delta.z = 0f;
        // Giải thích dòng gốc 119: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        float distance = delta.magnitude;
        // Giải thích dòng gốc 120: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(_cutSweepStep, 0.01f)));
        // Giải thích dòng gốc 121: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
        bool releasedAny = false;

        // Giải thích dòng gốc 123: Bắt đầu vòng lặp for với biến đếm hoặc điều kiện lặp rõ ràng.
        for (int i = 1; i <= steps; i++)
        // Giải thích dòng gốc 124: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
        {
            // Giải thích dòng gốc 125: Khai báo hoặc gán giá trị cho biến/trường để dùng ở các bước xử lý sau.
            Vector3 samplePosition = Vector3.Lerp(from, to, i / (float)steps);
            // Giải thích dòng gốc 126: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
            releasedAny |= TextureBlockSpawner.ReleaseAtWorldForActiveSpawners(samplePosition, _pressDirection,
                // Giải thích dòng gốc 127: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                _pressSpeed, _compressionForce, _bladeTangentialForce, _bladeSpinDirection, _bladePushRadius,
                // Giải thích dòng gốc 128: Dòng này tiếp tục cấu trúc code hiện tại và đóng góp vào logic của file.
                _sideDampingOnContact, _maxBlockVelocity);
        // Giải thích dòng gốc 129: Đóng khối code hiện tại.
        }

        // Giải thích dòng gốc 131: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
        return releasedAny;
    // Giải thích dòng gốc 132: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 134: Khai báo hàm ClearCache với tham số trong ngoặc để thực hiện một hành vi.
    private void ClearCache(Collider other)
    // Giải thích dòng gốc 135: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 136: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        _chunkCache.Remove(other);
    // Giải thích dòng gốc 137: Đóng khối code hiện tại.
    }

    // Giải thích dòng gốc 139: Khai báo hàm GetChunk với tham số trong ngoặc để thực hiện một hành vi.
    private TextureBlockChunk GetChunk(Collider other)
    // Giải thích dòng gốc 140: Mở một khối code mới cho khai báo hoặc câu lệnh phía trên.
    {
        // Giải thích dòng gốc 141: Kiểm tra điều kiện; chỉ chạy khối bên trong khi điều kiện đúng.
        if (_chunkCache.TryGetValue(other, out TextureBlockChunk chunk))
            // Giải thích dòng gốc 142: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
            return chunk;

        // Giải thích dòng gốc 144: Cập nhật giá trị cho biến, trường, thuộc tính hoặc phần tử dữ liệu.
        chunk = other.GetComponentInParent<TextureBlockChunk>();
        // Giải thích dòng gốc 145: Gọi hàm để thực hiện thao tác hoặc kích hoạt logic liên quan.
        _chunkCache.Add(other, chunk);
        // Giải thích dòng gốc 146: Trả kết quả về cho hàm gọi hoặc kết thúc hàm hiện tại.
        return chunk;
    // Giải thích dòng gốc 147: Đóng khối code hiện tại.
    }
// Giải thích dòng gốc 148: Đóng khối code hiện tại.
}
