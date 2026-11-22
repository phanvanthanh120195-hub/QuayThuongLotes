# Hướng dẫn đọc code LuckyDraw

File này ghi các **luồng chính**: đọc từ hàm nào, gọi sang hàm nào, theo thứ tự.
- `A → B`: A gọi B.
- `[file]`: hàm nằm trong file đó.
- Đường dẫn file tính từ `LuckyDraw/LuckyDraw.Web/`.

---

## 0. Bản đồ thư mục

| Thư mục / file | Vai trò |
|---|---|
| `Program.cs` | Khởi động app: đăng ký service, đăng nhập cookie, đa ngôn ngữ, route, SignalR |
| `Controllers/HomeController.cs` | Trả về trang màn chiếu |
| `Controllers/Api/*` | API cho màn chiếu: xem dữ liệu, quay số, vắng mặt, xuất Excel |
| `Areas/Admin/Controllers/*` | Các trang quản trị |
| `Services/*` | Nghiệp vụ: quay số, import, lưu file, nhật ký, đẩy realtime |
| `Data/Repositories/*` | Câu SQL (Dapper + Npgsql), mỗi bảng một repository |
| `Models/Settings/EventSettings.cs` | Cấu trúc theme / âm thanh lưu dạng JSON trong bảng `event` |
| `Hubs/DrawHub.cs` | Kênh SignalR (rỗng: server chỉ đẩy xuống, màn chiếu chỉ nghe) |
| `Views/Home/Index.cshtml` | HTML màn chiếu, chữ đa ngôn ngữ cho JS (`LD`), nạp các file JS |
| `wwwroot/js/draw.js` | **Toàn bộ logic màn chiếu** |
| `wwwroot/js/stage-effects.js` | Pháo hoa, giấy màu, tuyết, lấp lánh (`StageFx`) |
| `wwwroot/js/cursor-fx.js` | Hiệu ứng kèm con trỏ: đũa thần, hào quang, rồng (`CursorFx`) |
| `wwwroot/css/stage.css` | Giao diện màn chiếu |
| `Resources/translations.tsv` | Bản dịch; chạy `python tools/build_resx.py` để sinh `.resx` |

---

## 1. Khởi động app — `Program.cs`

`builder.Services.Add...`: đăng ký DbConnectionFactory, các Repository, DrawService, ImportService, RealtimeNotifier, AdminEventContext, IFileStorage.
→ `AddHostedService<AdminSeeder>`: tạo tài khoản admin lần đầu, mật khẩu lấy từ `Seed:AdminPassword`.
→ `app.UseRequestLocalization`: ngôn ngữ vi / zh-TW / zh-CN / en.
→ `MapControllerRoute("areas")`: các trang `/admin/...`.
→ `MapControllerRoute("default")`: trang `/` (màn chiếu).
→ `MapHub<DrawHub>("/hubs/draw")`: kênh realtime.

---

## 2. Mở màn chiếu (tải trang lần đầu)

**Server:**
`HomeController.Index` → `EventRepository.GetActiveAsync` → `Views/Home/Index.cshtml`
- Trang dựng HTML khung: tiêu đề, ô số, nút, bảng, các popup.
- Ghi chữ đa ngôn ngữ vào `window.LD`.
- Nạp lần lượt `stage-effects.js` → `cursor-fx.js` → `draw.js`.

**Client, cuối `draw.js`:**
```
StageFx.init                      [stage-effects.js] tạo canvas pháo hoa
→ bind()                          gắn sự kiện nút, phím tắt (Space/Enter, ←/→, F, M, Esc), resize
→ Sound.renderButton()
→ loadAll()
     → api('/api/view/event')     → ViewApiController.GetEvent
                                      → EventRepository.GetActiveAsync
                                      → ParticipantRepository.CodesAsync → CodeShape (độ dài mã, ký tự từng vị trí)
     → applyTheme()               màu, font, nền, logo, kiểu nút
          → applyCursor()         con trỏ theo theme (+ vẽ lại theo cỡ) → CursorFx.setMode
          → StageFx.setSpeed / setAmbient
     → Sound.configure()
     → refreshData(true)
          → api('/api/view/prizes')   → ViewApiController.GetPrizes  → PrizeRepository.ListByEventAsync
          → api('/api/view/results')  → ViewApiController.GetResults → ResultRepository.ListAllActiveAsync
          → buildSlots(n)         dựng ô số (kiểu glass / gold / drum)
               → sizeSlots()      tìm cỡ ô lớn nhất vừa màn hình
          → renderAll()           → renderPrize → renderButton → renderResults → sizeSlots
→ connectHub()                    nghe SignalR (mục 5)
```

---

## 3. Quay số (luồng quan trọng nhất)

**Bắt đầu:** bấm nút hoặc phím Space/Enter → `onStart()`
```
onStart
→ buildSlots(p.nextTake) → startSpin() → reelFrame()   (lặp mỗi khung hình)
                                         → boxRender()  (kiểu trống: → drumPlace)
→ StageFx.spin(true)                     pháo hoa trong lúc quay
→ api POST '/api/draw'  (chạy song song, đã có kết quả từ lúc bắt đầu)
```

**Server quay:**
```
DrawApiController.Draw
→ DrawService.DrawAsync          thử lại nếu 2 giải trùng người (unique index)
  → DrawOnceAsync                (1 transaction)
     → DrawRepository.LockPrizeAsync     khoá dòng giải
     → DrawRepository.CountActiveAsync   đã trúng bao nhiêu
     → DrawRepository.GetPoolAsync       danh sách người đủ điều kiện (thâm niên, chưa trúng…)
     → DrawService.PickRandom            Fisher–Yates + số ngẫu nhiên mật mã
     → DrawRepository.InsertResultsAsync
     → AuditRepository.InsertAsync       nhật ký
     → COMMIT
  → RealtimeNotifier.ResultChanged       báo các màn chiếu khác
  → ResultRepository.ListBatchAsync      trả danh sách người trúng
```

**Dừng:** bấm lần 2, hoặc tự dừng nếu giải kiểu "tự động" (`drawType 2`, hẹn giờ `spinSeconds`) → `doStop()`
```
doStop
→ (chờ kết quả API) → lockAll(winners)
     → desync()           các ô tách nhịp
     → land()             từng cột hãm lại, trái → phải
     → finishLand()       nảy + loé sáng
→ reveal(res)
     → StageFx.burst      pháo chúc mừng
     → showCongrats → showLastFull(winners, 'draw')   màn chúc mừng toàn màn hình
     → refreshData(false)
```

**Màn chúc mừng:**
- `[Xác nhận]` → `closeCongrats()` → `refreshData()` (hoặc `loadAll()` nếu trong lúc quay Admin đã đổi cấu hình)
- `[Bỏ qua]` (chỉ MC, chỉ khi lượt có 1 người) → `skipWinner()` → `confirmAbsent()` (popup) → `closeCongrats()` → api POST `/api/results/{id}/absent` (mục 4)

**Lỗi API khi đang quay:** `state.req.catch` → `abortSpin()` → `toast()` báo lỗi.

---

## 4. Vắng mặt (huỷ kết quả để quay lại người khác)

```
Nút "Vắng mặt" trong bảng (renderResults) → markAbsent → confirmAbsent → doMarkAbsent
(hoặc [Bỏ qua] ở màn chúc mừng → skipWinner)
→ api POST '/api/results/{id}/absent'
→ ResultApiController.MarkAbsent
   → ResultRepository.MarkAbsentAsync   đánh dấu kết quả bị huỷ + người vắng mặt
   → RealtimeNotifier.ResultChanged
→ refreshData(true)                     giải có lại 1 suất → quay tiếp
```

---

## 5. Realtime (Admin sửa → màn chiếu tự cập nhật)

```
Admin lưu (giải, giao diện, âm thanh, người tham dự, import…)
→ RealtimeNotifier.ConfigChanged / ResultChanged / ActiveEventChanged   [Services/RealtimeNotifier.cs]
→ SignalR /hubs/draw
→ draw.js connectHub():
     'ConfigChanged'      → loadAll()          (đang quay thì hoãn: state.needReload)
     'ResultChanged'      → refreshData(false) (đang quay thì hoãn: state.needRefresh)
     'ActiveEventChanged' → location.reload()
→ Việc bị hoãn được làm trong closeCongrats()
```
Tìm nơi gửi tín hiệu: tìm chuỗi `Realtime.` trong `Areas/Admin/Controllers`.

---

## 6. Các nút công cụ trên màn chiếu

| Nút | Luồng |
|---|---|
| Giải thưởng | `openPrizes` (dùng `state.prizes` đã có, không gọi API) |
| Người tham dự | `openPeople` → `loadPeople(page)` → `/api/view/participants` → `ViewApiController.GetParticipants` → `ParticipantRepository.SearchAsync` → `renderPager` |
| Kết quả tổng | `openSummary` → `renderSummary`; nút tải → `/api/results/export` → `ResultApiController.Export` → `ResultExporter.Build` |
| Kết quả vừa quay | `openLast` → `/api/view/results/last` → `ViewApiController.GetLastBatch` → `showLastFull(winners, 'last')` |
| Toàn màn hình | `toggleFullscreen`; `poke()` đếm giờ để ẩn chuột và làm mờ nút, ô số, menu sau 10 giây |
| Âm thanh | đối tượng `Sound`: `configure` / `update` / `sfx` / `loop` / `duck` / `toggle` |

---

## 7. Admin → Giao diện (mẫu chung cho mọi trang Admin)

```
GET  ThemeController.Index → WorkingEventAsync (AdminEventContext.GetAsync: sự kiện đang chọn trong Admin)
                           → View Theme/Index.cshtml (JS update() vẽ khung xem trước khi bạn chỉnh)
POST ThemeController.Index
     → Sanitize()                        kiểm tra / giới hạn mọi giá trị
     → IFileStorage.SaveAsync            ảnh nền, logo
     → EventRepository.UpdateThemeAsync  lưu theme_json
     → RealtimeNotifier.ConfigChanged    → màn chiếu loadAll → applyTheme (mục 5)
```

**Muốn thêm một tuỳ chọn giao diện mới, sửa đúng 6 chỗ theo thứ tự:**
1. `Models/Settings/EventSettings.cs`, lớp `ThemeSettings`: thêm thuộc tính và giá trị mặc định.
2. `ThemeController.cs`: thêm danh sách lựa chọn vào `ThemeOptions` (nếu có), rồi thêm dòng trong `Sanitize()`. **Quên bước này thì giá trị không được lưu.**
3. `Areas/Admin/Views/Theme/Index.cshtml`: thêm ô nhập, và cập nhật hàm `update()` nếu cần xem trước.
4. `wwwroot/js/draw.js`, hàm `applyTheme()`: đọc `t.tenThuocTinh` (tên camelCase).
5. `wwwroot/css/stage.css`: thêm CSS.
6. `Resources/translations.tsv`: thêm bản dịch, rồi chạy `python tools/build_resx.py`.

---

## 8. Import Excel

```
ImportController.Upload  → ImportService.SaveTempAsync (lưu file tạm, trả token)
→ ImportController.Preview → ImportService.PreviewAsync
     → MapHeader / Normalize            nhận tên cột song ngữ ("Mã số thẻ", "工號"…)
     → ValidateParticipant / ValidatePrize / ValidateDepartment   (mỗi dòng)
→ ImportController.Commit → ImportService.PreviewAsync (đọc lại) → CommitAsync (upsert) → DeleteTemp
→ RealtimeNotifier.ConfigChanged
```
Mẫu file Excel: `ImportService.BuildTemplate`.

---

## 9. Ảnh người tham dự hàng loạt

```
ParticipantsController.Photos(files)   (nhiều ảnh hoặc 1 file ZIP)
→ ParticipantRepository.PhotoMapAsync  bảng mã NV → người
→ Match(map, tên file)                 "VN005621 - Phan Van Thanh.jpg" → VN005621
→ IFileStorage.SaveAsync               (thư mục photo)
→ ParticipantRepository.SetPhotoAsync  → xoá ảnh cũ
→ RealtimeNotifier.ConfigChanged
```

---

## 10. Đăng nhập & phân quyền

```
AccountController.Login (POST)
→ UserRepository.GetByNameAsync → PasswordHasher.VerifyHashedPassword
→ HttpContext.SignInAsync (cookie) → UserRepository.TouchLoginAsync → nhật ký → RedirectAfterLogin
```
- Trang Admin kế thừa `AdminControllerBase`, có sẵn `WorkingEventAsync()`, `Success()` / `Error()` (thông báo), `NoEvent()`.
- MC được quay hay không: `CanDraw` trong `ViewApiController` → trả về trong `/api/view/event` → `LD.canDraw` ở JS.

---

## 11. Kết quả trong Admin

| Thao tác | Luồng |
|---|---|
| Xoá / Khôi phục | `ResultsController.Delete` / `Restore` → `ResultRepository.SoftDeleteAsync` / `RestoreAsync` → `ResultChanged` |
| Reset toàn bộ | `ResultsController.Reset` → `ResultRepository.ResetAllAsync` |
| Xuất Excel | `ResultsController.Export` → `ResultExporter.Build` |

---

## 12. Hiệu ứng (chỉ phần hiển thị, không ảnh hưởng dữ liệu)

- **`stage-effects.js`** (`StageFx`)
  - Hàm gọi từ ngoài: `init` / `setSpeed` / `setAmbient` / `spin` / `burst`.
  - Bên trong: `Fireworks` → `Shell` → `createBurst`; vòng vẽ `loop` / `ensureRunning`.
- **`cursor-fx.js`** (`CursorFx`)
  - Hàm gọi từ ngoài: `setMode('wand' | 'halo' | 'dragon', cỡ)`.
  - Bên trong: vòng vẽ `frame`, gọi các hàm vẽ `wandStep` (tia lửa), `drawHalo` + `drawRipples` (hào quang), `drawDragon` (rồng: `dragonLoad` → `dragonPlace` → `dragonStep`).
- **Ô quay số trong `draw.js`:** `reelSeq` → `boxRender` / `boxStatic` (kiểu trống: `drumPlace`).

---

## 13. Những chỗ nên xem lại (thêm / bớt / sửa / xoá)

| # | Chỗ | Đề xuất |
|---|---|---|
| 1 | `draw.js`, dòng đầu hàm `renderPrize()` có lệnh **`debugger`** | **Xoá trước ngày sự kiện.** Khi mở DevTools (F12), trang dừng lại mỗi lần vẽ tên giải. |
| 2 | `LuckyDraw.Web/LuckyDraw.sln` (file rỗng) | **Xoá.** Nó làm `dotnet build` / `dotnet run` trong thư mục `LuckyDraw.Web` báo lỗi MSB1011 và gây lỗi IIS Express trong VS. Solution thật là `LuckyDraw/LuckyDraw.sln`. |
| 3 | `cursor-fx.js`, phần rồng | Đang là bản **rồng đuổi theo / lượn quanh ngọc** (`dragonStep`), không phải bản **bò đúng theo đường chuột**. Nếu bạn muốn bản bò theo đường chuột thì cần làm lại. |
| 4 | `skipWinner()` và `doMarkAbsent()` | Hai hàm lặp cùng một đoạn gọi API vắng mặt. Có thể rút gọn: `skipWinner` chỉ cần `closeCongrats(); doMarkAbsent(w);`. |
| 5 | Danh sách con trỏ khai báo **2 nơi**: `CURSORS` trong `draw.js` và `ThemeOptions.Cursors` (+ `CursorFile`) trong `ThemeController.cs` | Thêm hoặc sửa con trỏ phải sửa cả hai, điểm bấm phải khớp nhau. |
| 6 | Chức năng **Bộ phận** (`DepartmentsController`, `DepartmentRepository`, import loại Departments) | Đang ẩn khỏi menu nhưng vẫn giữ code, theo quyết định trước. Không dùng hẳn thì có thể xoá sau. |
| 7 | `translations.tsv` | Còn 1 khoá chưa dịch (thông báo đường dẫn ảnh giải thưởng). Kiểm tra bằng `python tools/build_resx.py --check`. |
