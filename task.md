# BlockCrusher - Nhật ký công việc và cơ chế hiện tại

> Cập nhật: 2026-08-06
> Branch: `feature/tunadev_v3`  
> Commit nền: `2881ec6` (`fix water`)  
> Trạng thái worktree trước khi tạo file: sạch

File này là tài liệu bàn giao chính của project. Khi sửa gameplay, hãy cập nhật đúng một trong ba mục: **Đang làm dở**, **Đã làm**, hoặc **Đã hoàn tác**. Chỉ đánh dấu hoàn tất sau khi đã chạy đúng case trong Unity Play Mode.

## 1. Đang làm dở / cần kiểm tra lại

### Shader dây cáp của cần cẩu

- Trạng thái: `Đã thêm và basic Play Mode đã kiểm tra; full movement/suction regression còn lại`.
- Owner: `Assets/_Project/Script/Game/GamePlay/Crane/CraneHoseVisual.cs` và `Assets/_Project/Resources/Prefab/Crane/BlockCrane.prefab`.
- Mục tiêu: thay material mặc định của `LineRenderer` bằng shader Built-in có shading mềm theo bề ngang — sáng nhẹ ở giữa, tối dần ở hai mép như ảnh mẫu — không thay đổi mô phỏng/IK/path hút.
- Đã kiểm tra: shader import thành công, Console có 0 error/warning mới, `LineRenderer` runtime dùng `CraneCable (Instance)`, width thực tế `0.36`, có đủ 19 điểm dây và endpoint bounds hợp lệ trong `Gameplay` Play Mode.
- Còn lại: mở `Gameplay`, quan sát thêm khi tool di chuyển, đổi sang suction và kéo dây dài/ngắn; xác nhận alpha suction vẫn hoạt động.

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

### Map Painter gom prefab vào parent Blocks

- Trạng thái: `Đã sửa code và compile/self-check; cần dùng trực tiếp tool để xác nhận hierarchy khi paint/save level`.
- Owner: `Assets/_Project/Editor/MapPainterWindow.cs`.
- Đã sửa: prefab mới được tạo dưới một object cha `Blocks`; các lệnh generate, randomize, recolor và erase vẫn nhận cả block mới trong container lẫn block phẳng của level cũ.
- Đã kiểm tra: Unity compile không có error, `validate_script` không có diagnostic, và `Tools/Map Painter Self Check` pass.
- Còn lại: mở Map Painter, paint/generate vài block rồi save level; xác nhận hierarchy hiển thị `level_x/Blocks/Block_*`.

### Saw / drill tool switch and stone collision

- Status: `Code and Assembly-CSharp compile checked; Unity Play Mode verification pending because the Unity MCP session became unavailable during editor transition`.
- Drill reuses the existing `Gameplay/BlockCrane/Saw/Drill` object and cycles through `Saw -> Suction -> Drill -> Saw` from `SwitchCrane`.
- Only the drill cutter has `_canBreakStone = 1`; saw collision includes breakable stone for target clamping but does not apply crack damage.
- Released resources are spawned outside saw clearance and the saw push job repels overlapping resources in all radial directions.
- Next Play Mode case: click switch twice to select drill, drill a stone until it breaks; return to saw and confirm it stops at the stone with no crack; cut dense resource around the saw and confirm the blade center stays clear.

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
- Lưỡi cưa quay visual trong `LateUpdate`; chuyển động và cắt chạy trong `FixedUpdate`.
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
- Cưa va chạm obstacle qua Unity Physics/DOTS. Với obstacle thường, target bị chặn tại bề mặt; với đá breakable, chỉ gây damage và không đẩy cưa lệch.
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

- 2026-08-06: thay mosaic `BlockShape` không kín bằng 12 mesh `TerrainFragment_00..11` mới trong `Assets/_Project/Resources/Mesh/TerrainTileable`. Pattern 4x3 dùng chung chính xác bốn vertex trên mỗi cạnh và lặp tuần hoàn cả X/Y; context check trả `seams=True`. `Gameplay/LevelMapSpawner` đã được gắn đủ 12 mesh, static chunk và released ECS dùng cùng mesh/type cùng world-cell scale. Play Mode level 3 hiển thị terrain liền, không còn đường kẻ ô; kéo saw từ `(0,-3,107.08)` xuống `(0,-4.8,107.08)` tạo rãnh có biên bất quy tắc, giảm solid từ 1320 xuống 1292 và sinh đúng 28 Dirt entity từ 9 fragment type, 28/28 có collider. Released block dùng box collider đã bake `renderScale`, inset còn 78% XY và bevel 20% cạnh ngắn để tránh các biên fragment móc nhau nhưng vẫn cho phép xoay quanh Z. Stress Play Mode thả 561 cell tạo thành đống có mặt trên gồ ghề; snapshot có 657 Dirt entity, `657 Box`, 657 rotation enabled, 609 đã xoay, không có state NaN và Console sạch. Build C# đạt 0 error với 13 warning MSB3277 nền.
- 2026-08-06 (đã được thay thế): `Gameplay/LevelMapSpawner` từng được setup `BlockShape_01..12`. Static và released dùng cùng mesh/scale nhưng topology giữa các BlockShape không chia sẻ cạnh nên map vẫn lộ seam; dữ liệu type/vertex khớp không đủ chứng minh tessellation.
- 2026-08-06 (đã được thay thế): bản trung gian từng render terrain bằng box greedy-merge và chỉ dùng `BlockShape` cho debris. Bản này compile/Play Mode đạt 0 error nhưng không bảo đảm hình mảnh khớp biên lỗ, nên đã được thay bằng fitted-fragment flow ở dòng trên.
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
- 2026-08-05: Điều khiển theo video đã được xác nhận trong Play Mode sạch: idle giữ `Direction=(0,0)` thì saw `spinEnabled=False`, không có suction transit và `sucked=0`; chạm joystick cho `Direction=(0.47,0)`, tạo `transit=1` rồi block đi hết tube với `sucked=1`.
- 2026-08-05: Kéo saw vào obstacle giữ đầu cưa tại khoảng `y=-6.59`; sau một lần cắt có 255 released block, vận tốc ngang trung bình `0.031` và lớn nhất `0.749`, với lực tiếp tuyến/vận tốc prefab đã giảm và damping block đã tăng.

## 6. Những gì đã hoàn tác

### Vùng collider tròn cố định quanh tâm saw/drill

- Người dùng yêu cầu hoàn tác thử nghiệm ngày 2026-08-06; đã gỡ persistent saw clearance, projection vị trí và post-physics circle constraint.
- Giữ nguyên cơ chế `SawPushJob` cũ chỉ đẩy block khi cắt, cùng toàn bộ terrain tileable và box collider xoay của released block.

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

## 10. 2026-08-05 - Suction tube smoothness

- Trạng thái: `Đã sửa code, chờ kiểm tra đúng case block chạy hết ống trong Play Mode`.
- Owner: `Assets/_Project/Script/Game/GamePlay/ReleasedBlock/System/ReleasedBlockInteractionJob.cs`.
- Đã sửa: suction transit tìm target look-ahead xuyên qua các segment kế tiếp, loại vận tốc lệch khỏi hướng ống và giữ block bám segment khi crane di chuyển; capture/collider-off/collection giữ nguyên.
- Đã kiểm tra: Unity refresh + compile sạch, Console 0 error/warning; `dotnet build .\Assembly-CSharp.csproj --no-restore` đạt 0 error với các warning MSB3277 nền.
- Còn lại: trong `Gameplay` Play Mode bật suction, hút Dirt/Rock/ore/Water và quan sát block đi liên tục qua toàn bộ joint path; chưa đánh dấu hoàn tất vì MCP hiện chưa có thao tác joystick/input để chạy đúng case tự động.
### Crystal dính với đất khi cắt

- Trạng thái: `Đã làm xong`.
- Mục tiêu: khi saw release cell đất có crystal, crystal đi cùng mảnh đất và không spawn resource crystal riêng.
- Play Mode case: cắt qua cell đất có crystal; xác nhận crystal còn dính trên mảnh đất và không tăng resource crystal trước khi mảnh đất đi qua máy hút.
- Verification: `dotnet build .\Assembly-CSharp.csproj --no-restore` passed with 0 errors; Unity Console returned 0 error/warning entries. Play Mode was entered and SawHead moved through the runtime terrain, but MCP editor execution could not inspect the runtime ECS World, so the crystal attachment visual still needs a direct human Play Mode confirmation.
- Latest runtime verification: after the fresh Play Mode cut, the released-block query reported 18 dirt entities with 19 attachment entries; primary types were Water/Dirt and attached types were Blue_ore/Orange_ore/Purple_ore. No crystal primary physics entities were created. The attachment render query ran without the previous missing-buffer exception.
- Console note: the only post-capture exception was the existing Advanced FPS Counter null reference in `Assets/Plugins/CodeStage/AdvancedFPSCounter/Runtime/Scripts/CountersData/Abstract/BaseCounterData.cs:216`; it is unrelated to the attachment flow.
- Status correction: the earlier note about MCP being unable to inspect the runtime ECS World is superseded by the latest direct Play Mode ECS query above; this task's attachment behavior is verified.
- Attachment visual flicker fix: dirt and its crystal attachment were submitted at the same Z while using opaque ZWrite/ZTest materials, allowing depth fighting. `PrepareRenderFrameJob` now applies a render-only `-0.005f` depth offset to attachment records; physics position, attachment ownership, and collection behavior are unchanged.
- Verification: Unity recompiled with no compile errors; the Play Mode cut case produced 26 released entities with attachment buffers and 28 attachment entries. The console was clean immediately after the cut case; the known Advanced FPS Counter issue remains unrelated.
- Crystal layout correction: each released dirt entity now stores every source crystal's relative position, rotation, and authored visual scale instead of placing all crystals at the dirt center with one uniform released scale.
- Verification: `dotnet build .\Assembly-CSharp.csproj --no-restore` passed with 0 errors; the exact Play Mode cut case produced 18 dirt entities with 19 attachments, all 19 with varied authored scales, and the console returned 0 error/warning entries after the cut.
- Release classification correction: `BlueShard`, `OrangeShard`, and `PurpleShard` remain dirt attachments. `Crystal_0_Blue`, `Crystal_0_Orange`, and `Crystal_0_Purple` now use separate `TypeBlock` enum values and follow the old independent released-resource spawn path.
- Runtime verification: the shard cut produced only ore attachment types (`Blue_ore:5`, `Orange_ore:7`, `Purple_ore:7`) with `Crystal_0` attachment count 0. A separate `Crystal_0_Orange` cut produced 40 `Crystal_0_Orange` primary resource entities and 0 `Crystal_0` attachments. Console returned 0 error/warning entries in both cases.

### Water render layer fix

- Status: `Code fixed; runtime wiring verified`.
- Cause: the metaball effect camera culls only layer 8 (`Metaball`), while the runtime `Metaball Water Source` could remain on layer 0, so the water field was not captured by the effect camera.
- Fix: `LevelMapSpawner.Water` assigns the source particle object to the `Metaball` layer immediately before spawning water particles.
- Verification: fresh Play Mode reported `sourceLayer=8`, `effectMask=256`, `waterCount=158`, `particles=158`; Unity Console had 0 error/warning entries. `dotnet build .\Assembly-CSharp.csproj --no-restore` passed with 0 errors and the existing MSB3277 warnings.

### Saw released block lift

- Status: `Code fixed; runtime verified`.
- Behavior: `_releasedBlockLiftSpeed` is the intentional upward force applied when the space above the cut is open, so saw-released Dirt is pushed upward instead of immediately dropping down.
- Change: restored the serialized field, scene value, and `ApplyReleaseLift` call; normal gravity remains enabled after the upward push. Released blocks keep Z-axis inertia and receive angular velocity from the saw tangential force, so they spin naturally during the cut and fall.
- Verification: fresh Play Mode saw cut produced 17 released blocks. All 17 had gravity enabled, settled in `y=-13.128..-12.593`, and reported `maxAbsAngular=1.672` immediately after the cut (`1.195` after one second). Unity Console returned 0 error/warning entries. `dotnet build .\Assembly-CSharp.csproj --no-restore -v:q` passed with 0 errors and only the existing MSB3277 warnings.
