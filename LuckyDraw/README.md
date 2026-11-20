# LuckyDraw – Quay thưởng cuối năm

ASP.NET Core 8 MVC + Dapper/Npgsql + PostgreSQL (Supabase) + SignalR. Thiết kế chi tiết: [../thiet-ke-luckydraw.md](../thiet-ke-luckydraw.md).

**Đã xong:** phần Admin (`/admin`), màn chiếu quay số (`/`), API quay, "Vắng mặt – quay lại", SignalR.

## Màn chiếu (trang chính `/`)

- Ai cũng xem được. MC (Operator) hoặc Admin đăng nhập mới có nút **Bắt đầu**. Đặt `"LuckyDraw": { "PublicView": false }` trong appsettings nếu muốn bắt đăng nhập mới xem.
- Kết quả do server quyết định ngay khi bấm Bắt đầu (`POST /api/draw`). Màn chiếu chỉ diễn hiệu ứng rồi dừng đúng mã đã quay.
- **Thủ công:** bấm Bắt đầu, rồi bấm Dừng. **Tự động:** tự dừng sau "thời gian quay". "Thời gian dừng" là lúc các ô khoá dần từ trái sang phải.
- Giải quay nhiều người một lượt thì hiện nhiều dãy ô cùng lúc, rồi đến màn chúc mừng dạng lưới.
- Bảng kết quả có nút **Vắng mặt**: huỷ kết quả, đánh dấu người đó vắng mặt, tiến độ giảm 1. Bấm Bắt đầu sẽ quay đúng 1 người thay thế.
- Giao diện (chủ đề, nền, logo, font, ô số, nút, hiệu ứng, vị trí bảng kết quả) và âm thanh lấy từ trang Admin. Admin lưu xong là màn chiếu tự cập nhật qua SignalR.
- Trình duyệt chỉ cho phát nhạc sau lần chạm hoặc bấm phím đầu tiên, nên trên màn hình có nhắc "Chạm vào màn hình để bật nhạc".

| Phím | Tác dụng |
|---|---|
| Space / Enter | Bắt đầu, Dừng, đóng màn chúc mừng |
| ← / → | Giải trước / giải sau |
| F | Toàn màn hình (chuột và thanh công cụ tự ẩn) |
| M | Tắt / bật âm thanh |
| Esc | Đóng màn chúc mừng, hộp thoại |

## Chạy lần đầu

> Hướng dẫn đầy đủ từng bước (Supabase, cấu hình, chạy trong mạng LAN, bản Release, sao lưu, xử lý sự cố): **[../HUONG-DAN-SETUP.md](../HUONG-DAN-SETUP.md)**.

1. **Database.** Mở Supabase → SQL Editor, chạy lần lượt:
   1. `database/01_schema.sql`
   2. `database/02_seed.sql` (dữ liệu mẫu, chỉ dùng để test)
   3. `database/03_app_role.sql` (sửa mật khẩu `DOI_MAT_KHAU_TRUOC_KHI_CHAY` trước khi chạy, **không commit** bản có mật khẩu thật)
   4. `database/04_participant_info.sql` (chỉ cần khi nâng cấp database cũ; cài mới thì `01` đã có các cột này)
2. **Chuỗi kết nối và mật khẩu admin.** Để trong User Secrets, không để trong `appsettings.json`:
   ```
   cd LuckyDraw.Web
   dotnet user-secrets set "ConnectionStrings:LuckyDraw" "Host=aws-?-ap-northeast-1.pooler.supabase.com;Port=5432;Database=postgres;Username=luckydraw_app.<project-ref>;Password=<mật khẩu>;SSL Mode=Require;Search Path=luckydraw"
   dotnet user-secrets set "Seed:AdminPassword" "<mật khẩu admin, tối thiểu 8 ký tự>"
   ```
   Lần đầu khởi động, nếu bảng `app_user` còn trống, app tự tạo tài khoản `admin` với mật khẩu ở trên.
3. **Chạy.**
   ```
   dotnet run
   ```
   Mở http://localhost:5280/admin.

## Các trang Admin

| Trang | Chức năng |
|---|---|
| Tổng quan | Số liệu sự kiện, tiến độ từng giải, cảnh báo giải không đủ người hợp lệ, kết quả vừa quay, các bước chuẩn bị |
| Sự kiện | Tạo, sửa, xoá (gõ lại tên để xác nhận); **Chiếu** (chỉ 1 sự kiện được chiếu); chọn sự kiện để quản lý ở thanh trên cùng |
| Giải thưởng | Tên 4 ngôn ngữ, số lượng, số người mỗi lượt, kiểu quay (thủ công / tự động), thời gian quay và dừng, thâm niên min/max, ảnh, bật/tắt, đổi thứ tự |
| Người tham dự | Tìm kiếm, lọc theo bộ phận và trạng thái (chưa trúng / đã trúng / vắng mặt), thâm niên tính theo ngày sự kiện, đánh dấu vắng mặt, xoá danh sách |
| Bộ phận | *Đang ẩn khỏi menu* (bộ phận giờ ghi thẳng trên từng người tham dự). Trang `/admin/departments` vẫn còn |
| Import Excel | File mẫu → upload → xem trước, báo lỗi từng dòng → xác nhận, ghi trong 1 transaction (upsert theo mã) |
| Giao diện | Chủ đề, ảnh nền (làm mờ, phủ tối), logo, font và màu tiêu đề, ô quay số, nút bắt đầu, hiệu ứng, lời chúc 4 ngôn ngữ, **xem trước trực tiếp** → `event.theme_json` |
| Âm thanh | Thư viện mp3, nhạc nền (không phát / 1 danh sách / mỗi giải 1 bài), hiệu ứng (đang quay, hiện kết quả, chúc mừng, bấm nút), âm lượng → `event.sound_json` |
| Kết quả | Tab Tất cả / Đã xoá, xoá mềm, **khôi phục** (`POST /admin/results/{id}/restore`), **reset** (`POST /admin/results/reset`, gõ lại tên sự kiện), tải .xlsx |
| Tài khoản | Admin / Operator (MC); không tự hạ quyền mình, luôn còn ít nhất 1 Admin |
| Nhật ký | Mọi thao tác thay đổi dữ liệu (role `luckydraw_app` chỉ INSERT/SELECT được bảng này) |

Đổi ngôn ngữ (Tiếng Việt / 繁體中文 / 简体中文 / English) ở góc trên phải. Ngôn ngữ lưu trong cookie, riêng cho từng trình duyệt.

## Bản dịch

Khoá dịch chính là câu tiếng Việt viết trong code (`L["Giải thưởng"]`). Cách sửa hoặc thêm bản dịch:

1. Sửa `LuckyDraw.Web/Resources/translations.tsv`.
2. Chạy lại script để sinh lại các file `.resx`:
   ```
   python tools/build_resx.py
   ```
   Script cũng liệt kê những khoá trong code chưa có bản dịch.

## Bảo mật đã áp dụng

- Mọi câu SQL đều dùng tham số và nằm trong `Data/Repositories`. Mọi `UPDATE` / `DELETE` đều lọc theo khoá chính và `event_id`.
- Mật khẩu băm bằng `PasswordHasher<T>`. Antiforgery bật cho mọi POST. Người thao tác lấy từ `User.Identity.Name`, không nhận từ form.
- Upload:
  - Chỉ nhận `.jpg .jpeg .png .webp .mp3`, có kiểm tra cả chữ ký đầu file.
  - Đổi tên file thành GUID và lưu ở `wwwroot/uploads/{eventId}/...`.
  - Khi backup, phải sao lưu **cả thư mục này lẫn DB**.
- Giá trị giao diện và âm thanh chỉ nhận trong danh sách cho phép. Đường dẫn file luôn lấy từ server.

## API

| Method | URL | Quyền |
|---|---|---|
| GET | `/api/view/event`, `/api/view/prizes`, `/api/view/results?prizeId=`, `/api/view/results/last`, `/api/view/participants?q=&page=` | Ai cũng xem |
| POST | `/api/draw` `{ "prizeId": 1 }` | Operator, Admin + header `RequestVerificationToken` |
| POST | `/api/results/{id}/absent` | Operator, Admin + header `RequestVerificationToken` |

## Trước ngày sự kiện

- Chạy thử một buổi quay hoàn chỉnh trên máy chiếu, **bằng mạng thật của nơi tổ chức**.
- Sau khi quay thử: Admin → Kết quả → **Reset kết quả**.
- Supabase gói Free tự tạm dừng nếu 7 ngày không dùng. Trước 1–2 ngày phải mở dashboard kiểm tra, và tự export backup.
