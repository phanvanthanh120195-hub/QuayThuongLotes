# Thiết kế hệ thống Quay thưởng cuối năm (LuckyDraw) — v2 (Supabase)

> Stack: **ASP.NET Core 8 MVC + SignalR + Dapper/Npgsql + PostgreSQL trên Supabase**.
> Script database nằm trong thư mục `database/` (đã chạy thử trên PostgreSQL 16). Phần C# là thiết kế, chưa có code.

---

## 0. Các quyết định đã chốt

| # | Câu hỏi | Quyết định | Ảnh hưởng tới thiết kế |
|---|---|---|---|
| 1 | Tiếng Trung | **Cả phồn thể và giản thể** | Thành **4 ngôn ngữ**: `vi-VN`, `zh-TW`, `zh-CN`, `en-US` |
| 2 | Cách hiển thị | **Chọn ngôn ngữ nào thì hiện ngôn ngữ đó** | Mỗi trình duyệt tự chọn (lưu cookie); màn chiếu và Admin độc lập |
| 3 | Người trúng vắng mặt | **Quay lại ngay trên trang chính** | MC (role Operator) có nút "Vắng mặt – quay lại"; xoá mềm kết quả |
| 4 | Ảnh / nhạc | **Lưu tạm ở local server** | `wwwroot/uploads/...` và bọc qua interface `IFileStorage` |
| 5 | Database | **Supabase (PostgreSQL trên cloud)** | Viết lại schema bằng cú pháp PostgreSQL |
| 6 | Cách gọi API | **Phương án A: C# API → Supabase** | Supabase chỉ đóng vai trò database; trình duyệt **không** gọi thẳng Supabase |

> ⚠️ **Còn mở:** công ty có cho phép dữ liệu nhân viên lên cloud không? Nếu Supabase chỉ dùng để học/demo, sau này chuyển về SQL Server nội bộ thì chỉ phải sửa tầng `Data/` (mọi câu SQL đều nằm ở đó).

---

## 1. Kiến trúc tổng thể

```
 Máy chiếu (MC)            Máy IT (Admin)
 ┌───────────────┐         ┌───────────────┐
 │ Trang chính / │         │ /admin/...    │
 │ draw.js       │         │ CRUD, Import  │
 └──────┬────────┘         └──────┬────────┘
        │ HTTP (API quay, xem)    │ HTTP (form, upload)
        │ ◄── SignalR push ───────┤
 ┌──────▼─────────────────────────▼────────┐
 │       LuckyDraw.Web (ASP.NET Core)       │
 │ Controllers │ Areas/Admin │ Api │ Hubs   │
 │ Services: DrawService, ImportService,    │
 │           IFileStorage, AuditService     │
 │ Data: Dapper + Npgsql (SQL có tham số)   │
 └──────────────────┬───────────────────────┘
                    │ Internet, SSL, role luckydraw_app
                    ▼
     Supabase ─ Session pooler :5432 ─ PostgreSQL
                (schema "luckydraw")
```

**Vì sao chọn phương án A (C# API) mà không cho trình duyệt gọi thẳng Supabase?**
- Chuỗi kết nối và mật khẩu nằm trên server, trình duyệt không thấy được.
- Logic quay (transaction, khoá dòng, random) giữ ở C#, đúng như thiết kế v1.
- Bạn được học và viết C#/.NET, đúng với hướng phát triển.

**Vì sao dùng Dapper mà không dùng EF Core?**
- Giống cách làm ở công ty: tự viết SQL có tham số, tự thấy và tự kiểm soát từng câu.
- Logic quay cần `FOR UPDATE` và transaction, viết thẳng SQL thì dễ đọc hơn.
- Bạn đã dùng Npgsql trong MyCostWeb, nên chỉ cần học thêm Dapper (một thư viện mỏng nằm trên Npgsql).

---

## 2. Phân quyền

| Role | Được làm |
|---|---|
| `Admin` | Toàn bộ `/admin`: sự kiện, giải, bộ phận, người tham dự, import, giao diện, âm thanh, kết quả (reset, khôi phục), tài khoản |
| `Operator` (MC) | Trang chính: bấm quay, đánh dấu **vắng mặt – quay lại** |
| Chưa đăng nhập | Chỉ xem trang chính (tuỳ chọn, có thể tắt) |

- Dùng Cookie Authentication. Mật khẩu băm bằng `PasswordHasher<T>` (có salt), **không dùng MD5**.
- `[Authorize(Roles="Admin")]` đặt ở mức Area. Các API thay đổi dữ liệu cần `[Authorize(Roles="Admin,Operator")]` + `[ValidateAntiForgeryToken]`.
- Lấy user từ `User.Identity.Name` phía server, **không nhận user từ form gửi lên**.
- Tài khoản admin đầu tiên: tạo bằng một lệnh seed trong C# lúc khởi động (đọc mật khẩu từ User Secrets). Không làm bằng SQL được, vì mật khẩu phải được băm bằng C#.

---

## 3. Database (PostgreSQL / Supabase)

### 3.1. Các file script

| File | Nội dung | Chạy ở đâu |
|---|---|---|
| `01_schema.sql` | Schema `luckydraw`, 7 bảng, index, ràng buộc, bật RLS | Supabase → SQL Editor |
| `02_seed.sql` | Dữ liệu mẫu: 5 bộ phận, 1 sự kiện, 6 giải, 60 người | Chỉ dùng để test |
| `03_app_role.sql` | Role `luckydraw_app` riêng cho app C# | Sau khi đổi mật khẩu trong file |

### 3.2. Sơ đồ quan hệ

```
department 1───* participant *───1 event 1───* prize
                     │                │          │
                     └──────* draw_result *──────┘
app_user (đăng nhập)          audit_log (nhật ký)
```

### 3.3. Khác với bản SQL Server (v1)

| SQL Server | PostgreSQL | Ghi chú |
|---|---|---|
| `INT IDENTITY` | `INT GENERATED ALWAYS AS IDENTITY` | |
| `NVARCHAR(n)` | `VARCHAR(n)` | Postgres mặc định UTF-8, chữ Hán và tiếng Việt lưu bình thường (đã test) |
| `BIT` | `BOOLEAN` | Trong SQL viết `NOT is_removed`, không viết `= 0` |
| `DATETIME2` | `TIMESTAMPTZ` | Lưu theo UTC, khi hiển thị đổi sang giờ VN |
| `UNIQUEIDENTIFIER` | `UUID` | C# dùng `Guid` |
| `NVARCHAR(MAX)` chứa JSON | `JSONB` | Postgres kiểm tra JSON hợp lệ, truy vấn được bên trong |
| Filtered index | Partial index `WHERE NOT is_removed` | |
| `WITH (UPDLOCK, HOLDLOCK)` | `SELECT ... FOR UPDATE` | |
| `DATEADD(MONTH, -n, d)` | `d - make_interval(months => n)` | |
| `PascalCase` | `snake_case` | Postgres tự đổi tên không có ngoặc kép sang chữ thường. Dapper map sang C# bằng `DefaultTypeMap.MatchNamesWithUnderscores = true` |

### 3.4. Các lớp bảo vệ dữ liệu trong DB (đã test)

| Ràng buộc | Chặn được lỗi gì |
|---|---|
| `ux_draw_result_one_win` (partial unique) | 1 người trúng 2 giải, kể cả khi code có bug hoặc 2 máy bấm cùng lúc. Người bị xoá mềm (vắng mặt) thì không tính |
| FK kép `(prize_id, event_id)`, `(participant_id, event_id)` | Kết quả gắn với giải/người của **sự kiện khác** |
| `ck_result_removed` | Xoá mềm mà thiếu lý do, thời điểm hoặc người xoá |
| FK `draw_result → participant` (không cascade) | Xoá người đã trúng giải |
| `ux_event_one_active` | Hai sự kiện cùng đang chiếu |
| `ck_prize_seniority` | Thâm niên tối thiểu ≥ tối đa |
| RLS bật, chỉ có policy cho `luckydraw_app` | Role `anon`/`authenticated` của Supabase không đọc được dữ liệu, kể cả khi lỡ tay GRANT |
| `luckydraw_app` chỉ có `SELECT, INSERT` trên `audit_log` | App không sửa/xoá được nhật ký |

### 3.5. Vì sao đặt bảng trong schema `luckydraw` chứ không để ở `public`

Supabase tự sinh REST API cho các bảng trong schema `public`. Phương án A không dùng REST API này. Đặt bảng ở schema riêng nên các bảng **không bị lộ ra API công khai**. RLS là lớp bảo vệ thứ hai.

### 3.6. Cách tính thâm niên

```sql
p.join_date <= (@event_date - make_interval(months => @min))::date   -- thâm niên >= min
p.join_date >  (@event_date - make_interval(months => @max))::date   -- thâm niên <  max
```

- Sự kiện ngày 01/12 → mốc 6 tháng là 01/06: vào làm 01/06 là **đủ** 6 tháng, vào làm 02/06 là **chưa đủ**.
- Tháng thiếu ngày được tự xử lý: 31/08 trừ 6 tháng ra 28/02 (đã test).
- Với dữ liệu mẫu: Giải khuyến khích (< 6 tháng) có 10 người hợp lệ, Giải nhất/đặc biệt (≥ 6 tháng) có 50 người, cộng lại đủ 60.

### 3.7. JSON giao diện (ví dụ)

```json
{
  "themeKey": "tet-red-gold",
  "backgroundPath": "/uploads/1/bg/8f2c....webp",
  "stageEffect": "fireworks",
  "congratsTitle": { "vi": "Chúc mừng", "zhTW": "恭喜", "zhCN": "恭喜", "en": "Congratulations" },
  "digitBox": { "radius": 30, "placeholder": "?", "fontFamily": "Anton" },
  "startButton": { "style": 2, "label": { "vi": "Bắt đầu", "zhTW": "開始", "zhCN": "开始", "en": "Start" } }
}
```

---

## 4. Kết nối C# → Supabase

### 4.1. Chọn chuỗi kết nối nào

| Loại | Port | Dùng khi | Hệ thống này |
|---|---|---|---|
| Direct connection | 5432 | Mạng có IPv6 | ❌ Mạng VN đa số chỉ có IPv4 |
| **Session pooler** | **5432** | Backend chạy liên tục, mạng IPv4 | ✅ **Dùng cái này** |
| Transaction pooler | 6543 | Serverless (không hỗ trợ prepared statement) | ❌ |

Lấy chuỗi: Supabase Dashboard → nút **Connect** → **Session pooler**.

### 4.2. Chuỗi kết nối Npgsql

```
Host=aws-?-ap-northeast-1.pooler.supabase.com;Port=5432;Database=postgres;
Username=luckydraw_app.<project-ref>;Password=<mật khẩu>;
SSL Mode=Require;Search Path=luckydraw
```

- `Host` và `<project-ref>` lấy đúng theo dashboard; **không đoán**.
- Lúc mới dev có thể dùng `postgres.<project-ref>`. Trước khi chạy thật phải chuyển sang `luckydraw_app`.
- **Không để mật khẩu trong `appsettings.json`** mà để trong User Secrets:
  `dotnet user-secrets set "ConnectionStrings:LuckyDraw" "Host=...;Password=..."`
- `Search Path=luckydraw` giúp viết SQL ngắn gọn (`prize` thay vì `luckydraw.prize`). Tuy vậy, trong code vẫn nên ghi đầy đủ tên schema cho rõ ràng.

### 4.3. Giới hạn gói Free

- Project **tự tạm dừng nếu ít hoạt động trong 7 ngày**. Dữ liệu không mất, bấm *Resume* là chạy lại. Trước ngày sự kiện 1–2 ngày phải mở dashboard kiểm tra.
- **Không có backup tự động.** Trước sự kiện, tự export (Dashboard → Database → Backups, hoặc `pg_dump`).
- **Phụ thuộc Internet.** Nên có phương án dự phòng, ví dụ: in sẵn danh sách và quay bằng file Excel.
- Server đặt ở Tokyo, độ trễ từ VN khoảng vài chục ms, không ảnh hưởng tới việc quay.

---

## 5. Logic quay (DrawService) — phần quan trọng nhất

### 5.1. Luồng

```
POST /api/draw { prizeId }
 1. BEGIN
 2. Khoá dòng giải:  SELECT ... FROM luckydraw.prize WHERE prize_id=@id FOR UPDATE
    → máy thứ 2 bấm cùng lúc phải CHỜ (đã test: phiên 2 chờ đến khi phiên 1 commit)
 3. Đếm kết quả còn hiệu lực → remaining = quantity - active_count
    remaining = 0 → ROLLBACK, báo "Giải đã đủ người"
 4. take = MIN(draw_per_turn, remaining)    ← quay lại người vắng mặt thì chỉ quay 1
 5. Lấy pool người hợp lệ (SQL 5.2)
    pool < take → ROLLBACK, báo "Không đủ người hợp lệ"
 6. Chọn ngẫu nhiên take người trong C# (RandomNumberGenerator + Fisher–Yates)
 7. INSERT draw_result (cùng batch_id) + INSERT audit_log
 8. COMMIT
 9. SignalR: ResultChanged(eventId, prizeId)
10. Trả kết quả về draw.js → chạy hiệu ứng ô số rồi dừng đúng mã đã quay
```

**Điểm then chốt:** kết quả được quyết định ở **server trước**, client chỉ diễn hiệu ứng.

### 5.2. SQL

```sql
-- Bước 2: khoá giải + lấy cấu hình (trong transaction)
SELECT p.prize_id, p.event_id, p.quantity, p.draw_per_turn,
       p.min_seniority_months, p.max_seniority_months, e.event_date
FROM luckydraw.prize p
JOIN luckydraw.event e ON e.event_id = p.event_id
WHERE p.prize_id = @prize_id AND p.is_active AND e.is_active
FOR UPDATE OF p;

-- Bước 3: số người đã trúng giải này
SELECT count(*) FROM luckydraw.draw_result
WHERE prize_id = @prize_id AND NOT is_removed;

-- Bước 5: pool hợp lệ
SELECT p.participant_id, p.employee_code, p.full_name, p.department_id
FROM luckydraw.participant p
WHERE p.event_id = @event_id
  AND NOT p.is_absent
  AND (@min::int IS NULL OR p.join_date <= (@event_date - make_interval(months => @min::int))::date)
  AND (@max::int IS NULL OR p.join_date >  (@event_date - make_interval(months => @max::int))::date)
  AND NOT EXISTS (SELECT 1 FROM luckydraw.draw_result r
                  WHERE r.event_id = p.event_id
                    AND r.participant_id = p.participant_id
                    AND NOT r.is_removed);
```

> ⚠️ Khi tham số có thể là `NULL` (như `@min`, `@max`), phải ép kiểu `@min::int`. Nếu không, Npgsql/Postgres có thể báo lỗi "could not determine data type of parameter".

### 5.3. Vì sao không dùng `new Random()` hay `ORDER BY random()`

| Cách | Vấn đề |
|---|---|
| `new Random()` | Không đủ ngẫu nhiên cho việc cần công bằng |
| `ORDER BY random()` trong SQL | Chạy được, nhưng khó kiểm thử và khó giải thích khi có người thắc mắc |
| **`RandomNumberGenerator.GetInt32(n)`** ✅ | Ngẫu nhiên mật mã, có sẵn trong .NET; dùng với Fisher–Yates để chọn k người không trùng |

---

## 6. Luồng "Vắng mặt – quay lại" (trên trang chính)

```
MC mở "Kết quả vừa quay" → bấm "Vắng mặt – quay lại" ở người đó
 → Hộp xác nhận: "Đánh dấu Nguyễn Văn A vắng mặt? Người này sẽ không được quay nữa."
 → POST /api/results/{resultId}/absent
     BEGIN
       UPDATE luckydraw.draw_result
          SET is_removed = TRUE, removed_reason = 1, removed_at = now(), removed_by = @user
        WHERE result_id = @id AND NOT is_removed
        RETURNING participant_id;          -- không trả về dòng nào → đã xử lý rồi → ROLLBACK
       UPDATE luckydraw.participant SET is_absent = TRUE WHERE participant_id = @pid;
       INSERT INTO luckydraw.audit_log (...) VALUES (..., 'MARK_ABSENT', ...);
     COMMIT
 → SignalR ResultChanged → tiến độ giảm từ "6/6" về "5/6"
 → MC bấm BẮT ĐẦU → take = MIN(draw_per_turn, 1) = 1 → quay đúng 1 người thay thế
```

- `RETURNING` trong Postgres thay cho `@@ROWCOUNT` của SQL Server: biết được có dòng nào bị sửa không, đồng thời lấy luôn `participant_id`.
- Bấm nhầm thì **Admin** có nút *Khôi phục*. Trang chính không có nút này.
- **Reset toàn bộ kết quả** chỉ có ở Admin, phải gõ lại tên sự kiện để xác nhận.

---

## 7. Đa ngôn ngữ (4 thứ tiếng)

### 7.1. Chữ cố định của giao diện → `.resx`

```
Resources/SharedResource.vi.resx | .zh-TW.resx | .zh-CN.resx | .en.resx
```

- `Program.cs`: `AddLocalization` + `UseRequestLocalization`, khai báo đủ 4 culture, mặc định `vi-VN`.
- Đổi ngôn ngữ: `POST /Account/SetLanguage` → ghi cookie `.AspNetCore.Culture` → quay lại trang cũ.
- Chữ dùng trong JS thì render `window.i18n = {...}` trong layout, **không viết cứng chữ trong file .js**.

### 7.2. Dữ liệu trong DB → chọn cột theo ngôn ngữ

```
GetName(vi, zhTw, zhCn, en):
  zh-TW → zhTw ?? vi
  zh-CN → zhCn ?? zhTw ?? vi     (chưa có giản thể thì tạm dùng phồn thể)
  en    → en   ?? vi
  khác  → vi
```

### 7.3. Những điểm hay bị bỏ sót

- **Font:** Anton hay Bebas Neue không có chữ Hán → `font-family: Anton, "Microsoft JhengHei", "Microsoft YaHei", sans-serif;`
- Đặt `<html lang="zh-TW">` / `"zh-CN"` đúng theo ngôn ngữ.
- Chuyển phồn → giản tự động chỉ chuyển từng chữ, không chuyển đúng từ vựng (軟體 → đúng phải là 软件). Admin cần xem lại.

---

## 8. Lưu file upload (local)

```
wwwroot/uploads/{eventId}/bg/ | prize/ | music/
```

- `IFileStorage` (Save, Delete, GetUrl) với cài đặt `LocalFileStorage`. Sau này muốn dùng **Supabase Storage** thì viết thêm `SupabaseFileStorage`.
- Chỉ cho phép đuôi `.jpg .jpeg .png .webp .mp3`, **không cho `.svg`, `.html`**. Giới hạn dung lượng. Đổi tên file thành `Guid`. DB chỉ lưu đường dẫn tương đối.
- Khi backup: backup **cả DB lẫn thư mục `uploads`**.

---

## 9. Import Excel

Thư viện: **ClosedXML** (giấy phép MIT).

| File mẫu | Cột |
|---|---|
| Người tham dự | `employee_code*`, `full_name*`, `department_code`, `join_date*` (yyyy-MM-dd) |
| Giải thưởng | `sort_order*`, `name_vi*`, `name_zh_tw`, `name_zh_cn`, `name_en`, `quantity*`, `draw_per_turn`, `draw_type`, `spin_seconds`, `stop_seconds`, `min_seniority_months`, `max_seniority_months` |
| Bộ phận | `code*`, `name_vi*`, `name_zh_tw`, `name_zh_cn`, `name_en` |

Luồng: **Tải mẫu → Upload → Xem trước, báo lỗi từng dòng → Xác nhận → Ghi DB trong 1 transaction.**

- Postgres có sẵn **upsert**: `INSERT ... ON CONFLICT (event_id, employee_code) DO UPDATE SET ...` (cho chế độ *Ghi đè*) hoặc `DO NOTHING` (cho chế độ *Bỏ qua*). Dựa vào khoá unique nên không thể update nhầm cả bảng.
- Import qua Internet: gom nhiều dòng vào 1 lệnh (hoặc dùng `COPY` / binary import của Npgsql), **không gửi từng dòng một**. Mỗi lần gửi lên Tokyo mất vài chục ms, 1000 dòng gửi riêng lẻ sẽ rất chậm.

---

## 10. Đồng bộ realtime (SignalR)

| Sự kiện | Gửi khi | Trang chính làm gì |
|---|---|---|
| `ConfigChanged(eventId)` | Admin lưu giải / giao diện / âm thanh / người tham dự | Tải lại cấu hình |
| `ResultChanged(eventId, prizeId)` | Quay xong, vắng mặt, Admin khôi phục/reset | Tải lại tiến độ và bảng kết quả |
| `ActiveEventChanged` | Admin đổi sự kiện đang chiếu | Tải lại toàn trang |

Vẫn dùng SignalR, không dùng Supabase Realtime: với phương án A, mọi thay đổi đều đi qua C#, nên C# biết lúc nào cần báo cho màn chiếu.

---

## 11. Cấu trúc thư mục project

```
LuckyDraw/
├─ database/
│  ├─ 01_schema.sql
│  ├─ 02_seed.sql
│  └─ 03_app_role.sql            (bản có mật khẩu thật KHÔNG commit)
└─ LuckyDraw.Web/
   ├─ Program.cs                 (DI, auth, localization, SignalR, Dapper config)
   ├─ appsettings.json           (KHÔNG chứa mật khẩu DB)
   ├─ Controllers/
   │  ├─ HomeController.cs       → Trang chính
   │  ├─ AccountController.cs    → Login, Logout, SetLanguage
   │  └─ Api/  DrawApiController, ViewApiController, ResultApiController
   ├─ Areas/Admin/
   │  ├─ Controllers/  Dashboard, Events, Prizes, Departments, Participants,
   │  │                Import, Theme, Sound, Results, Users
   │  └─ Views/...
   ├─ Hubs/DrawHub.cs
   ├─ Services/  DrawService, ImportService, IFileStorage/LocalFileStorage,
   │             AuditService, LocalizedName
   ├─ Data/
   │  ├─ DbConnectionFactory.cs  (tạo NpgsqlConnection từ connection string)
   │  └─ Repositories/           (NƠI DUY NHẤT chứa SQL)
   ├─ Models/  Entities/, ViewModels/, Dtos/
   ├─ Resources/  SharedResource.{vi,zh-TW,zh-CN,en}.resx
   ├─ Views/
   └─ wwwroot/  js/draw.js, js/admin/, css/, uploads/ (không commit)
```

NuGet: `Npgsql`, `Dapper`, `ClosedXML`. SignalR có sẵn trong ASP.NET Core.

---

## 12. Danh sách API

| Method | URL | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/view/event` | Ai cũng xem | Sự kiện đang chiếu + theme + sound |
| GET | `/api/view/prizes` | Ai cũng xem | Danh sách giải + tiến độ X/N |
| GET | `/api/view/participants?q=&page=` | Ai cũng xem | Danh sách người tham dự, phân trang ở server |
| GET | `/api/view/results?prizeId=` | Ai cũng xem | Kết quả (lọc theo giải) |
| GET | `/api/view/results/last` | Ai cũng xem | Lượt quay gần nhất |
| POST | `/api/draw` | Operator, Admin | Quay 1 lượt |
| POST | `/api/results/{id}/absent` | Operator, Admin | Vắng mặt – quay lại |
| POST | `/admin/results/{id}/restore` | Admin | Khôi phục kết quả |
| POST | `/admin/results/reset` | Admin | Reset toàn bộ (cần xác nhận) |

---

## 13. Thứ tự làm đề xuất

1. Supabase SQL Editor: chạy `01_schema.sql` → `02_seed.sql` → (đổi mật khẩu) `03_app_role.sql`.
2. Project rỗng: `Program.cs`, kết nối DB (thử 1 câu `SELECT count(*) FROM luckydraw.participant` ra 60), đăng nhập, **đổi 4 ngôn ngữ**.
3. Admin: Sự kiện → Bộ phận → Giải thưởng → Người tham dự (CRUD).
4. Import Excel.
5. `DrawService` + **test** (đủ người, pool rỗng, 2 request đồng thời, thâm niên biên).
6. Trang chính + `draw.js` + SignalR.
7. Vắng mặt – quay lại, Admin khôi phục/reset.
8. Giao diện (theme JSON), âm thanh, upload.
9. Chạy thử một buổi quay hoàn chỉnh trên máy chiếu, **bằng mạng thật của nơi tổ chức**.

## 14. Checklist review trước khi dùng thật

- [ ] Mọi câu SQL đều dùng tham số, không ghép chuỗi
- [ ] Mọi `UPDATE`/`DELETE` đều có `WHERE` theo khoá và `event_id`
- [ ] API quay / vắng mặt có `[Authorize]` + chống CSRF
- [ ] Test 2 máy bấm quay cùng lúc → không trùng người, không vượt `quantity`
- [ ] Test người vào làm đúng ngày biên 6 tháng
- [ ] App kết nối bằng `luckydraw_app`, **không** bằng `postgres`
- [ ] Mật khẩu DB nằm trong User Secrets / biến môi trường, không có trong git
- [ ] Upload chặn `.svg/.html`, đổi tên file
- [ ] Màn chiếu hiển thị đúng chữ Hán ở cả zh-TW và zh-CN
- [ ] 1–2 ngày trước sự kiện: Supabase project đang chạy (không bị tạm dừng), đã export backup
- [ ] Có phương án dự phòng khi mất Internet
