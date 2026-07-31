# BlockCrusher - Task Handoff

File này là nguồn bàn giao công việc giữa các máy. Đọc file này trước khi tiếp tục làm project.

> Cập nhật lần cuối: 2026-07-31  
> Branch tại thời điểm cập nhật: `tunadev_v2.3.2`  
> Commit nền: `1b4a4cb va chạm joint obstacle`

## Quy tắc cập nhật

- Khi bắt đầu việc mới, thêm vào **Đang làm dở**.
- Ghi rõ mục tiêu, file liên quan, phần đã làm, phần còn lại và cách kiểm tra.
- Khi hoàn thành và đã kiểm tra, chuyển task sang **Đã hoàn thành**.
- Khi hoàn tác thay đổi, chuyển hoặc ghi task vào **Lịch sử hoàn tác**; không xóa dấu vết.
- Trước khi đổi máy, cập nhật ngày, branch, commit nền rồi commit và push file này cùng code.
- Không ghi mật khẩu, token, key hoặc dữ liệu nhạy cảm vào file.

## Đang làm dở

### Chưa xác định - các thay đổi local cần kiểm tra

- Trạng thái: `Cần xác nhận`
- Mục tiêu: Chưa có mô tả task từ phiên làm việc trước.
- Thay đổi đang tồn tại:
  - `Assets/_Project/Script/Game/GamePlay/Crane/CraneController.Fuel.cs`
    - Chỉ có thay đổi khoảng trắng cuối dòng.
  - `Assets/_Project/Script/Game/GamePlay/SpawnTexture/Jobs/ReleasedBlockSolidConstraintJob.cs`
    - Một số câu `if` được rút thành một dòng; chưa thấy thay đổi logic.
  - `Assets/_Project/Script/Game/GamePlay/SpawnTexture/TextureBlockSpawner.cs`
    - Chỉ có thay đổi khoảng trắng cuối dòng.
  - `Packages/manifest.json`
    - Thêm `com.unity.editorcoroutines` và `com.unity.visualeffectgraph`.
  - `Packages/packages-lock.json`
    - Cập nhật dependency tương ứng.
  - `ProjectSettings/VFXManager.asset`
    - Unity đã điền shader và runtime settings của Visual Effect Graph.
- Việc tiếp theo:
  - [ ] Xác nhận các package/VFX settings có chủ ý hay do Unity tự sinh.
  - [ ] Xác nhận các thay đổi format trong ba script có cần giữ lại.
  - [ ] Mở Unity, chờ compile xong và kiểm tra Console.
  - [ ] Chạy đúng Play Mode case liên quan nếu các thay đổi trên thuộc một task gameplay.
  - [ ] Cập nhật mục tiêu task thật vào đây trước khi sửa tiếp.

## Đã hoàn thành

Chưa có task nào được ghi nhận trong file handoff này.

## Lịch sử hoàn tác

Chưa có lần hoàn tác nào được ghi nhận.

Khi hoàn tác, thêm bản ghi theo mẫu:

### YYYY-MM-DD - Tên thay đổi đã hoàn tác

- Task liên quan:
- Lý do hoàn tác:
- Phạm vi file:
  - `Assets/...`
- Nội dung bị loại bỏ:
- Cách hoàn tác: ví dụ `git restore -- <file>` hoặc sửa tay có kiểm soát.
- Mốc trước/sau: commit, diff hoặc mô tả đủ để truy lại.
- Đã kiểm tra sau hoàn tác:
  - [ ] `git diff` chỉ còn thay đổi mong muốn.
  - [ ] Unity compile không có error mới.
  - [ ] Play Mode case liên quan hoạt động đúng.

## Mẫu task mới

### Tên task

- Ngày bắt đầu: YYYY-MM-DD
- Trạng thái: `Đang làm` / `Bị chặn` / `Chờ kiểm tra`
- Mục tiêu:
- Nguyên nhân gốc hoặc luồng sở hữu:
- File liên quan:
  - `Assets/...`
- Đã làm:
  - [ ]
- Còn lại:
  - [ ]
- Cách kiểm tra:
  - [ ] Unity compile không có error mới.
  - [ ] Mô tả Play Mode case cụ thể.
- Ghi chú để tiếp tục trên máy khác:

