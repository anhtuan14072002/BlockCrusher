# BlockCrusher - Nhật ký công việc và cơ chế hiện tại

> Cập nhật: 2026-08-04  
> Branch: `feature/tunadev_v3`  
> Commit nền: `2881ec6` (`fix water`)  
> Trạng thái worktree trước khi tạo file: sạch

File này là tài liệu bàn giao chính của project. Khi sửa gameplay, hãy cập nhật đúng một trong ba mục: **Đang làm dở**, **Đã làm**, hoặc **Đã hoàn tác**. Chỉ đánh dấu hoàn tất sau khi đã chạy đúng case trong Unity Play Mode.

## 1. Đang làm dở / cần kiểm tra lại

### Effect bụi khi máy cưa cắt block đang bị tắt tạm thời

- Trạng thái: `Đang tắt để tuning`, chưa phải tính năng đã xóa vĩnh viễn.
- `CreateCutParticles()` và `EmitCutParticles(...)` hiện là no-op trong `Assets/_Project/Script/Game/GamePlay/LevelMap/LevelMapSpawner.CutVisuals.cs`.
- Việc cắt cell, tạo block rời, physics và rebuild chunk vẫn chạy bình thường.
- Đã kiểm tra `git diff --check` ở lần sửa ban đầu; lần build khi đó bị timeout nên chưa có xác nhận Play Mode sau thay đổi này.
- Việc tiếp theo: quyết định bật lại, thay effect khác, hoặc xóa hẳn code particle; sau đó test cắt đất/quặng/đá trong Play Mode.

### Cắt theo mesh của đầu công cụ

- Trạng thái: `Code và compile đã kiểm tra; cần lặp lại Play Mode trên bản cuối`.
- Bản test runtime trước cleanup đã cắt đúng 8 cell với mesh `Rangcua` xoay 37 độ.
- Sau cleanup API cũ, build vẫn 0 error nhưng chưa chạy lại đúng live case trên bản cuối.
- Việc tiếp theo: vào `Gameplay`, cắt ở cạnh và góc mesh; xác nhận không cắt cell nằm ngoài hình mesh và không bỏ sót cell giao với răng cưa.

### Máy hút và nước sau các commit mới nhất

- Trạng thái: `Có trong code hiện tại; cần regression test Play Mode trên HEAD 2881ec6`.
- Các commit gần nhất là `c9f3dde fix máy hút` và `2881ec6 fix water`.
- Case cần chạy:
  - Máy hút quay theo hướng di chuyển, thân máy không xuyên terrain/obstacle.
  - Block vào vùng hút, đi theo đường miệng hút → các joint → đầu ống, rồi bị thu thập.
  - Dirt, Rock, các loại ore và Water tăng đúng bộ đếm.
  - Block đang ở trong ống không còn collider, không va chạm gây kẹt; block ngoài ống vẫn có physics.
  - Metaball water spawn đủ, hiển thị đúng và hút được.

### Thay đổi Level 3 xuất hiện trong lúc bàn giao

- Trạng thái: `Cần người đang sửa level xác nhận`, không được tự hoàn tác.
- Trước khi tạo file này, `git status --short` không có thay đổi.
- Trong lúc kiểm tra cuối, Unity/Map Painter phát sinh:
  - `Assets/_Project/Editor/MapPainterData/level_3.asset`: `Scale.x` đổi từ `0.5` thành `1`.
  - `Assets/_Project/Resources/Level/level_3.prefab`: prefab được generate lại với diff rất lớn.
- Đây không phải thay đổi do task tạo tài liệu thực hiện. Cần mở Level 3 kiểm tra hình dạng/scale rồi mới quyết định giữ hoặc hoàn tác.
- Không có script gameplay local chưa commit được phát hiện tại thời điểm cập nhật mục này.

## 2. Chức năng hiện có

### Level và Map Painter

- Level được author bằng prefab trong `Assets/_Project/Resources/Level` và dữ liệu tool trong `Assets/_Project/Editor/MapPainterData`.
- `MapPainterWindow` dùng để paint block/obstacle/decoration và lưu cấu hình từng level.
- `LevelMapSpawner` đọc các `TypeBlockMap` trong prefab, kiểm tra cell vuông và thẳng grid, rồi tạo dữ liệu cell bằng `NativeArray`.
- Obstacle mask các cell bị chiếm trước khi dựng map.
- Terrain được render theo chunk; chỉ chunk bẩn được rebuild và số chunk rebuild mỗi frame bị giới hạn bởi `_maxChunkRebuildsPerFrame`.
- `LoadLevel(int)` đổi level theo mảng `_levelPrefabs`; `Spawn()` dọn dữ liệu cũ rồi tạo map mới.

### Các loại block

Các ID tài nguyên hiện có trong `TypeBlock`:

| Loại | Ý nghĩa hiện tại |
|---|---|
| `Dirt` | Đất có thể cắt, tạo block rời và hút |
| `Purple_ore` | Quặng tím, có prefab/màu block rời riêng |
| `Blue_ore` | Quặng xanh, có prefab/màu block rời riêng |
| `Orange_ore` | Quặng cam, có prefab/màu block rời riêng |
| `Rock` | Đá; dùng cho block đá và mảnh đá vỡ |
| `Water` | Nước metaball; khi hút được tính là tài nguyên nước |
| `None` | Không phải vật phẩm có thể cộng vào kho |

Mỗi `TypeBlockMap` giữ các dữ liệu authoring: loại block, prefab khi được giải phóng, scale, cell size, màu trên map và màu block rời.

### Vòng đời của một block terrain

1. `LevelMapSpawner.Spawn()` chuyển prefab level thành grid `_cellSolid`, màu và type index.
2. `LevelMapMeshBuilder` và `BuildChunkMeshJob` dựng mesh theo chunk thay vì tạo một GameObject cho mỗi cell.
3. Khi cưa chạm cell, `ReleaseCell(...)` đặt cell thành rỗng, đánh dấu chunk bẩn và tạo một entity block rời.
4. Entity block rời nhận collider, mass, damping, vận tốc, giới hạn tốc độ và mặt phẳng Z cố định.
5. `ReleasedBlockPlanarConstraintJob`, continuous collision và solid constraint giữ block trong gameplay 2D, hạn chế xuyên terrain/tường và tránh đi quá xa trong một physics step.
6. Block rời có thể bị băng chuyền đẩy, máy hút thu, hoặc vùng clear hủy.
7. Khi tới cuối đường hút, entity bị hủy và `TypeBlock` tương ứng được cộng vào `SuckedItems`/UI nâng cấp.

### Block đất và quặng

- Đều dùng chung pipeline grid → released-block ECS.
- Mesh, material, collider, scale và collectible type được cache theo runtime type; không tạo MonoBehaviour riêng cho từng block rời.
- Block vừa cắt nhận lực hướng ra ngoài và lực tiếp tuyến theo chiều quay của lưỡi cưa.
- Vận tốc được đổi hướng nếu phía trước còn cell đặc, được thêm lực nâng nếu phía trên trống và bị clamp theo cell size/radius để giảm tunneling.
- Rendering block rời dùng batch tối đa 1023 instance và cập nhật theo `_releasedBlockRenderInterval`.

### Đá có thể phá

- `BreakableObstacle` nhận damage theo thời gian từ máy cưa, cập nhật `_CrackAmount` theo các crack stage.
- Khi đủ durability, đá tạo mặc định 18 mảnh qua cùng ECS released-block path.
- Mảnh đá có collider và solid constraint giống đất nhưng `UsesGravity = 0`, vì vậy có va chạm nhưng không rơi xuống.
- Máy cưa vẫn gây damage bằng sweep/overlap nhưng đá breakable không tham gia clamp/slide thông thường, tránh đẩy lệch đầu cưa.
- Case này đã được Play Mode kiểm tra: 36/36 mảnh có collider, 36/36 gravity bằng 0 và saw target không bị lệch.

### Decoration

- `LevelDecoration` có tint riêng và có thể cấu hình prefab/type/scale/màu khi được giải phóng.
- Decoration gắn với cell; khi cell bị cắt, decoration liên quan được giải phóng và có thể trở thành collectible.
- Decoration chunk cũng được đánh dấu bẩn cùng cell terrain.

### Nước

- `BlockWater` đánh dấu vùng nước trong prefab và làm cell terrain tương ứng thành rỗng.
- `LevelMapSpawner.Water` tạo entity nước, còn `ParticleSystem` chỉ đảm nhiệm hiển thị metaball theo vị trí entity.
- Entity nước không dùng gravity và có `CollectibleType = Water`.
- `Validate Metaball Water` kiểm tra marker, particle system và số lượng particle đã spawn.

## 3. Cơ chế máy cưa

- Owner đầu vào: `Assets/_Project/Script/Game/GamePlay/SawBlockCutter.cs`.
- Lưỡi cưa quay visual trong `Update`; chuyển động và cắt chạy trong `FixedUpdate`.
- Cưa chỉ bắt đầu release khi thực sự đã di chuyển sau `OnEnable`, tránh cắt nhầm lúc spawn/enable.
- Chuyển động nhanh được chia thành nhiều mẫu theo `_cutSweepStep`, tránh bỏ lọt cell giữa hai physics frame.
- Vùng cắt lấy trực tiếp từ `MeshFilter.sharedMesh`: bounds dùng cho broad phase, còn triangle mesh được kiểm tra overlap chính xác với ô cell.
- Vì dùng rotation và lossy scale của transform, thay mesh đầu cưa/đầu khoan có thể đổi hình cắt mà không cần thêm bán kính cắt terrain riêng.
- `_bladePushRadius` chỉ điều khiển lực tác động lên block rời, không quyết định cell nào bị cắt.
- Khi cắt:
  - Cell được xóa khỏi grid và snapshot collision.
  - Block/decoration rời được tạo.
  - Chunk liên quan được rebuild trì hoãn.
  - `SawPushJob` đẩy các block rời gần lưỡi cưa.
  - Resistance tạm thời giảm khả năng điều khiển rồi hồi phục dần.
- Cưa va chạm obstacle qua Unity Physics/DOTS. Với obstacle thường, target được clamp và trượt theo tiếp tuyến; với đá breakable, chỉ gây damage và không đẩy cưa lệch.
- Effect bụi cắt hiện đang tắt tạm thời như mục **Đang làm dở**.

## 4. Cơ chế cần cẩu và máy hút

### Cần cẩu

- `CraneController` là một `sealed partial MonoBehaviour`; các file partial chia theo `IK`, `Joints`, `Target`, `Fuel`, `RoundState`, `Setup` và utility nhưng vẫn là cùng một component/state.
- Joystick đặt target đầu công cụ trong mặt phẳng XY; IK giải chuỗi joint và giữ chiều dài segment.
- Joint và thân tool dùng collider query chung của `LevelObstacle`; khi gặp vật cản, chuyển động được chiếu lên tiếp tuyến để trượt thay vì xuyên hoặc khóa cứng.
- `AddJoint()` tăng tầm với bằng cách thêm joint theo chuỗi hiện tại. Các chỉnh sửa thứ tự/zigzag trước đây có phần chỉ compile-clean, vì vậy cần test trực quan nếu tiếp tục sửa chức năng này.
- Round flow quản lý thời gian/fuel, trạng thái bắt đầu-kết thúc round và UI liên quan.

### Máy hút

- Owner chính: `Assets/_Project/Script/Game/GamePlay/Crane/SuctionDevice.cs`.
- Vùng bắt block là oriented box đi từ miệng hút theo local `right`.
- `ProcessSuction()` tạo path bắt đầu tại miệng hút và nối tiếp qua đường ống/joint do `CraneController.AppendSuctionTubePath(...)` cung cấp.
- MonoBehaviour chỉ enqueue request; `ReleasedBlockInteractionSystem` xử lý block bằng Burst job trước physics.
- Khi block mới lọt vào vùng hút:
  - Collider bị bỏ.
  - Solid constraint bị tắt.
  - Z render được lùi vào trong ống.
  - Gravity bằng 0 và block chuyển sang `SuctionTransit`.
- Block bám theo từng segment với look-ahead, acceleration, max velocity và arrival damping.
- Khi tới waypoint cuối trong `_tubeExitRadius`, entity bị hủy và loại tài nguyên được đưa vào hàng đợi thu thập.
- Máy hút chỉ bắt block mới khi `allowCapture` cho phép, nhưng block đã ở trong ống vẫn tiếp tục đi hết path.
- Thân máy hút dùng box collider của `PhysicsShapeAuthoring`; target được depenetrate/clamp và trượt trên terrain/obstacle. Hướng đầu hút quay dần theo hướng di chuyển bằng `_rotationSpeedDegrees`.

### Băng chuyền và vùng clear

- `ConveyorCrane` enqueue request để tăng dần vận tốc block theo hướng băng chuyền.
- `CraneClear` enqueue request hủy entity block nằm trong bounds.
- Cả hai dùng chung `ReleasedBlockInteractionQueue`, không dò từng GameObject block bằng MonoBehaviour.

## 5. Những việc đã làm

- Chuyển terrain từ nhiều block GameObject sang grid/chunk mesh và released-block ECS để giảm GameObject, draw call và chi phí physics.
- Tạo level-prefab/Map Painter workflow để author block, obstacle, decoration và nước trong Editor.
- Thêm các loại Dirt, ba loại ore, Rock và Water cùng bộ đếm vật phẩm hút được.
- Cắt terrain theo mesh thật của đầu công cụ, có sweep theo chuyển động và exact mesh-cell overlap.
- Thêm lực cưa, lực tiếp tuyến, resistance, giới hạn vận tốc và xử lý block không xuyên cell đặc.
- Thêm obstacle collision/slide cho cưa, joint và thân máy hút.
- Thêm đá nứt/vỡ thành mảnh ECS có collider, không gravity và không làm lệch cưa.
- Thêm máy hút block đi theo đường ống/joint, tắt collider trong ống và cộng đúng loại tài nguyên khi thu xong.
- Thêm conveyor, vùng clear, water metaball, decoration release và UI vật phẩm/nâng cấp.
- Tách `CraneController` thành các file partial theo trách nhiệm mà không tạo thêm manager/service.
- Thêm các self-check/context menu cho level prefab, mesh-cell overlap, breakable setup và metaball water.

## 6. Những gì đã hoàn tác

### Thử nghiệm ép mảnh đá về cùng một mặt phẳng/layer

- Đã thử cho toàn bộ mảnh đá render cùng mặt phẳng để xử lý cảm giác layer/collision.
- Người dùng yêu cầu hoàn tác; thay đổi này đã được gỡ đúng phạm vi.
- Giữ lại chức năng đá nứt, vỡ 18 mảnh, collider, không gravity và hút được.
- Random Z nhỏ của block rời được khôi phục (`0.0001f`).

### Các thử nghiệm cảm giác cưa orbit/trigger-only/recoil

- Các prototype làm đầu cưa orbit, chỉ dùng trigger hoặc bật recoil khi thả joystick đã bị từ chối và hoàn tác.
- Luồng hiện tại giữ `SawBlockCutter` resistance/cutting và target control hiện có; không còn state recoil riêng từ các prototype đó.

### Chu kỳ tối ưu ReleasedBlock ngày 2026-07-22

- `b48f220`: revert thay đổi “tối ưu performance”.
- `bcb82e0`: reapply thay đổi đó.
- `df2d116`: revert lần reapply.
- Đây là lịch sử Git cũ; các system ReleasedBlock sau đó đã tiếp tục được sửa ở các commit mới, không được dùng ba commit này để suy luận trạng thái code hiện tại.

### UI Map Painter không đúng yêu cầu

- Control `Slot Count` từng được thêm theo suy đoán rồi đã bị xóa khi xác định đúng yêu cầu là chỉnh `Overlay Z`.
- Hiện giữ `Overlay Z` có serialize/migration; không giữ UI/persistence của `Slot Count`.

## 7. Quy tắc cập nhật file

- Bắt đầu task: thêm vào **Đang làm dở**, ghi mục tiêu, owner/file, phần đã làm, phần còn lại và Play Mode case.
- Hoàn tất: chuyển sang **Những việc đã làm** chỉ sau compile và đúng runtime case.
- Hoàn tác: ghi rõ nội dung bị gỡ, lý do, file/commit liên quan và hành vi nào được giữ lại.
- Không xóa lịch sử hoàn tác vì người làm sau cần biết thử nghiệm nào đã bị từ chối.
- Trước khi bàn giao máy khác: cập nhật ngày, branch, commit nền; chạy `git diff --check`; commit và push `task.md` cùng code.
- Không ghi token, key, mật khẩu hoặc dữ liệu nhạy cảm vào file này.

## 8. Checklist regression tối thiểu

- [ ] Unity compile xong, Console không có error mới.
- [ ] Spawn từng level, grid/chunk/obstacle/water đúng vị trí.
- [ ] Cưa đất và ore ở giữa, cạnh, góc; mesh cắt đúng hình và không bỏ lọt khi di chuyển nhanh.
- [ ] Cưa đá đến vỡ; cưa không bị lệch; mảnh đá có collider và không rơi.
- [ ] Block rời không xuyên terrain/tường ở vận tốc gameplay.
- [ ] Máy hút quay/slide đúng, hút block qua toàn bộ joint path và cộng đúng từng `TypeBlock`.
- [ ] Water metaball hiển thị và được thu thập đúng.
- [ ] Conveyor đẩy block; CraneClear xóa block.
- [ ] AddJoint, IK, fuel và round flow không bị regression.

## 9. Các file owner chính

- Level/grid/chunk: `Assets/_Project/Script/Game/GamePlay/LevelMap/LevelMapSpawner*.cs`
- Cắt và lực cưa: `Assets/_Project/Script/Game/GamePlay/SawBlockCutter.cs`, `LevelMapSpawner.Saw.cs`, `Jobs/SawPushJob.cs`
- Block ECS: `Assets/_Project/Script/Game/GamePlay/ReleasedBlock`
- Đá vỡ: `Assets/_Project/Script/Game/GamePlay/BreakableObstacle.cs`, `Assets/_Project/Script/Game/Block/LevelObstacle.cs`
- Cần cẩu/IK/máy hút: `Assets/_Project/Script/Game/GamePlay/Crane`
- Block authoring: `Assets/_Project/Script/Game/Block/TypeBlockMap.cs`, `LevelDecoration.cs`, `BlockWater.cs`
- Map Painter: `Assets/_Project/Editor/MapPainterWindow.cs`, `MapPainterLevelSettings.cs`
- Scene gameplay: `Assets/_Project/Scenes/Gameplay.unity`
