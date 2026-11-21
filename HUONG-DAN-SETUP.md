# Hướng dẫn cài đặt và vận hành LuckyDraw

Ứng dụng quay thưởng tiệc tất niên: trang **Admin** để chuẩn bị dữ liệu và **màn chiếu** để quay số trên máy chiếu.
Công nghệ: ASP.NET Core 8, PostgreSQL (khuyên dùng Supabase), SignalR.

Tài liệu này đi theo thứ tự: cài đặt (mục 1–5) → chuẩn bị sự kiện (mục 6) → ngày sự kiện (mục 7) → triển khai, sao lưu, xử lý sự cố (mục 8–11).

---

## 1. Chuẩn bị

| Cần có | Ghi chú |
|---|---|
| [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) trở lên | Kiểm tra: `dotnet --version` |
| Git | Để tải code về |
| Tài khoản [Supabase](https://supabase.com) (gói Free là đủ) | Hoặc một PostgreSQL 14+ của riêng bạn |
| Chrome hoặc Edge | Cho máy chiếu |
| Python 3 | *Không bắt buộc*. Chỉ cần khi sửa bản dịch (mục 11) |

```bash
git clone https://github.com/phanvanthanh120195-hub/QuayThuongLotes.git
cd QuayThuongLotes/LuckyDraw
```

Cấu trúc thư mục:

```
LuckyDraw/
├── database/          Script SQL (chạy theo thứ tự 01 → 04)
├── LuckyDraw.Web/     Ứng dụng web
│   └── wwwroot/
│       ├── images/    Ảnh giao diện (huy hiệu award.png, ảnh nền chủ đề)
│       └── uploads/   Ảnh/nhạc người dùng tải lên (KHÔNG có trong git, phải tự sao lưu)
└── tools/             Script sinh file bản dịch
```

---

## 2. Tạo database trên Supabase

### 2.1. Tạo project

1. Đăng nhập Supabase → **New project**.
2. Đặt tên, chọn vùng gần nhất (ví dụ *Northeast Asia (Tokyo)* hoặc *Southeast Asia (Singapore)*), đặt **Database password**. Mật khẩu này là của tài khoản `postgres`, **không** phải mật khẩu app dùng.

### 2.2. Chạy script SQL

Vào **SQL Editor** → **New query**, dán từng file trong `LuckyDraw/database/` rồi bấm **Run**, đúng thứ tự:

| Thứ tự | File | Việc của nó | Lưu ý |
|---|---|---|---|
| 1 | `01_schema.sql` | Tạo schema `luckydraw` và 7 bảng | ⚠️ **Xoá sạch schema `luckydraw` cũ.** Chỉ chạy lúc cài mới |
| 2 | `02_seed.sql` | Dữ liệu mẫu (1 sự kiện, 6 giải, 60 người) | *Không bắt buộc*, chỉ để chạy thử |
| 3 | `03_app_role.sql` | Tạo tài khoản database `luckydraw_app` cho app, bật bảo mật RLS | **Sửa `DOI_MAT_KHAU_TRUOC_KHI_CHAY` thành mật khẩu thật trước khi chạy** |
| 4 | `04_participant_info.sql` | Thêm cột tên tiếng Trung, bộ phận, chức vụ, ảnh cho người tham dự | Cài mới thì `01` đã có sẵn các cột này. Chạy file này khi **nâng cấp** database cũ. Chạy lại nhiều lần không sao |

> 🔒 Đừng lưu bản `03_app_role.sql` có mật khẩu thật vào git. Nếu cần giữ một bản, đặt tên kết thúc bằng `.local.sql` (ví dụ `03_app_role.local.sql`), vì `.gitignore` đã loại các file này.

### 2.3. Kiểm tra

Vào **Table Editor**. Ở ô chọn schema phía trên, đổi từ `public` sang **`luckydraw`**. Bạn sẽ thấy các bảng `event`, `prize`, `participant`, `draw_result`, `app_user`…

> Schema `public` trống là **bình thường**. App cố ý dùng schema riêng `luckydraw`, để dữ liệu không bị lộ qua API công khai mà Supabase tự mở cho `public`.

### 2.4. Lấy chuỗi kết nối

Bấm **Connect** (góc trên) → chọn **Session pooler** (cổng **5432**). Ghi lại:
- **Host**, dạng `aws-0-ap-northeast-1.pooler.supabase.com`
- **Project ref**: đoạn mã trong địa chỉ dashboard `supabase.com/dashboard/project/<project-ref>`

Tên đăng nhập của app là `luckydraw_app.<project-ref>` (có dấu chấm và project ref phía sau).

---

## 3. Cấu hình bí mật (User Secrets)

Chuỗi kết nối và mật khẩu admin **không** để trong `appsettings.json` hay trong git. Khi chạy ở máy phát triển, dùng User Secrets:

```bash
cd LuckyDraw/LuckyDraw.Web

dotnet user-secrets set "ConnectionStrings:LuckyDraw" "Host=<host>;Port=5432;Database=postgres;Username=luckydraw_app.<project-ref>;Password=<mật khẩu ở bước 2.2>;SSL Mode=Require;Search Path=luckydraw"

dotnet user-secrets set "Seed:AdminPassword" "<mật khẩu admin, tối thiểu 8 ký tự>"
```

- `Search Path=luckydraw` là **bắt buộc**, để app tìm thấy các bảng trong schema riêng.
- Mật khẩu có dấu `;` thì bọc trong dấu nháy đơn: `Password='abc;123'`.
- Xem lại các giá trị đã đặt: `dotnet user-secrets list`.

> ⚠️ **Chỉ thử kết nối 1 lần mỗi khi đổi cấu hình.** Supabase pooler **tạm chặn IP** nếu đăng nhập sai nhiều lần liên tiếp. Nếu gặp lỗi mật khẩu, đối chiếu lại chuỗi kết nối cho kỹ rồi mới chạy lại.

---

## 4. Chạy ứng dụng

```bash
cd LuckyDraw/LuckyDraw.Web
dotnet run
```

| Địa chỉ | Trang |
|---|---|
| http://localhost:5280 | Màn chiếu quay số |
| http://localhost:5280/admin | Trang quản trị |

**Lần chạy đầu tiên:** nếu chưa có tài khoản nào, app tự tạo tài khoản **`admin`** với mật khẩu `Seed:AdminPassword` ở bước 3. Đăng nhập xong, nên vào **Tài khoản** để đổi mật khẩu.

Nếu chưa đặt `Seed:AdminPassword`, cửa sổ chạy app sẽ báo *"Chưa có tài khoản nào…"* và không tạo admin. Đặt xong thì chạy lại.

---

## 5. Cho máy khác trong mạng LAN truy cập (máy chiếu, điện thoại)

Mặc định app chỉ nghe trên `localhost`. Để máy chiếu hoặc máy MC mở được:

```bash
dotnet run --urls http://0.0.0.0:5280
```

- Trên Windows, mở cổng 5280 trong tường lửa (*Windows Defender Firewall → Inbound Rules → New Rule → Port 5280*).
- Máy khác mở `http://<IP máy chạy app>:5280`. Xem IP bằng lệnh `ipconfig`.

---

## 6. Chuẩn bị sự kiện (trong trang Admin)

Làm theo thứ tự. Trang **Tổng quan** có danh sách các bước, bước nào xong sẽ được đánh dấu.

### 6.1. Sự kiện
**Sự kiện → Tạo sự kiện**: tên 4 ngôn ngữ, ngày sự kiện, địa điểm. **Ngày sự kiện** dùng để tính thâm niên.
Bấm **Chiếu** để sự kiện này hiện lên màn chiếu. Mỗi lúc chỉ có 1 sự kiện được chiếu.

### 6.2. Giải thưởng
Với mỗi giải: tên, số lượng, **số người mỗi lượt**, kiểu quay (*Thủ công*: MC bấm Dừng; *Tự động*: tự dừng sau "thời gian quay"), điều kiện thâm niên tối thiểu/tối đa (tháng), ảnh phần thưởng (không bắt buộc).
Thứ tự giải là thứ tự quay. Thường xếp giải nhỏ trước, **Giải đặc biệt cuối cùng**. Bảng "Thông tin giải thưởng" trên màn chiếu tự hiển thị từ giải cao nhất xuống.

### 6.3. Người tham dự: import file nhân sự
**Import Excel → Người tham dự**: upload thẳng file nhân sự, không cần đổi tiêu đề cột. App nhận các cột:

| Cột trong file | Bắt buộc |
|---|---|
| `工號 / Mã Số thẻ` | ✅ |
| `進廠日期 / Ngày vào làm` | ✅ |
| `Họ và tên` | ✅ |
| `姓名` (tên tiếng Trung) | |
| `Bộ phận …` | |
| `現任單位 …` (bộ phận tiếng Trung) | |
| `Chức Vụ …` | |

- Cột **STT** và **工齡** được bỏ qua. Thâm niên tự tính theo ngày sự kiện.
- **Ngày vào làm nên là ô ngày của Excel.** Nếu là chữ thì dùng dạng `yyyy-MM-dd` hoặc `dd/MM/yyyy`. Chữ kiểu `4/12/2021` sẽ bị hiểu là ngày 4 tháng 12.
- Có bước **xem trước**: lỗi được báo theo từng dòng. Còn dòng lỗi thì chưa cho ghi. Toàn bộ file được ghi trong một lần (lỗi thì không ghi dòng nào).
- Tối đa 5.000 dòng, 5 MB mỗi lần.

### 6.4. Ảnh chân dung (không bắt buộc)
**Người tham dự → Tải ảnh hàng loạt**: chọn nhiều ảnh hoặc **một file .zip**. Đặt tên file theo mã số thẻ:
`VN005621.jpg`, `VN005621 - Phan Van Thanh.jpg`, `VN005621_PhanVanThanh.png`… đều được.
JPG/PNG/WEBP, tối đa 5 MB mỗi ảnh. Ảnh vuông, mặt ở giữa là đẹp nhất. Ảnh hiện trên màn chúc mừng khi người đó trúng.

### 6.5. Giao diện và âm thanh
- **Giao diện:** chủ đề, ảnh nền, logo, font, kiểu ô số, nút bắt đầu, lời chúc 4 ngôn ngữ, hiệu ứng (sân khấu / khi đang quay / chúc mừng), **tốc độ bay của pháo hoa** (30–200%).
- **Âm thanh:** tải nhạc `.mp3`, chọn nhạc nền (chung hoặc mỗi giải một bài), hiệu ứng (đang quay, hiện kết quả, chúc mừng), âm lượng.

Lưu xong là màn chiếu đang mở **tự cập nhật**, không cần tải lại trang.

### 6.6. Tài khoản MC
**Tài khoản → Tạo tài khoản**, vai trò **Operator (MC)**. MC chỉ quay số được, không vào trang quản trị được.

---

## 7. Ngày sự kiện

### 7.1. Trước giờ bắt đầu
1. ✅ **Quay thử một buổi đầy đủ trên máy chiếu, bằng mạng thật của nơi tổ chức.**
2. ✅ Quay thử xong: **Admin → Kết quả → Reset kết quả** (gõ lại tên sự kiện để xác nhận).
3. ✅ Mở dashboard Supabase kiểm tra project còn chạy. Gói Free **tự tạm dừng sau 7 ngày không dùng**.

### 7.2. Trên máy chiếu
1. Mở `http://<máy chạy app>:5280`, bấm **Đăng nhập MC** ở góc phải.
2. Bấm biểu tượng **toàn màn hình** (góc trái) hoặc phím **F**. Không động chuột một lúc thì chuột và thanh công cụ tự ẩn.
3. **Chạm hoặc bấm vào màn hình một lần** để trình duyệt cho phép phát nhạc.

| Phím | Tác dụng |
|---|---|
| **Space / Enter** | Bắt đầu → Dừng → Xác nhận (đóng màn chúc mừng) |
| **← / →** | Giải trước / giải sau |
| **F** | Bật/tắt toàn màn hình |
| **M** | Tắt/bật âm thanh |
| **Esc** | Đóng hộp thoại, màn chúc mừng |

### 7.3. Khi người trúng vắng mặt
- Ngay trên màn chúc mừng: bấm **Bỏ qua**. Chỉ có khi lượt đó trúng 1 người.
- Hoặc trong bảng kết quả: bấm **Vắng mặt** ở dòng của người đó.

Hai cách đều mở popup xác nhận. Kết quả bị huỷ, người đó không được quay nữa, suất giải được trả lại. Bấm **Bắt đầu** để quay người thay thế.

### 7.4. Các nút trên màn chiếu
**Giải thưởng** (thông tin, tiến độ từng giải) · **Người tham dự** (tìm kiếm, 50 người/trang) · **Kết quả tổng** (tìm kiếm, lọc theo giải, tải Excel) · **Kết quả vừa quay** (mở lại màn chúc mừng gần nhất).

Kết quả do **máy chủ quyết định** ngay khi bấm Bắt đầu. Màn chiếu chỉ diễn hiệu ứng rồi dừng đúng mã đó. Mọi thao tác đều ghi vào **Nhật ký**.

---

## 8. Triển khai lâu dài (bản Release)

```bash
cd LuckyDraw/LuckyDraw.Web
dotnet publish -c Release -o ../publish
```

Khi chạy bản publish (môi trường **Production**), **User Secrets không được nạp**. Cấu hình bằng biến môi trường. Lưu ý: dấu `:` trong tên khoá đổi thành `__` (hai dấu gạch dưới).

Windows (PowerShell):
```powershell
cd ..\publish
$env:ConnectionStrings__LuckyDraw = "Host=...;Port=5432;Database=postgres;Username=luckydraw_app.<project-ref>;Password=...;SSL Mode=Require;Search Path=luckydraw"
$env:Seed__AdminPassword = "..."      # chỉ cần cho lần chạy đầu tiên
.\LuckyDraw.Web.exe --urls http://0.0.0.0:5280
```

Linux / macOS:
```bash
export ConnectionStrings__LuckyDraw="Host=...;...;Search Path=luckydraw"
dotnet LuckyDraw.Web.dll --urls http://0.0.0.0:5280
```

### Chạy bản build sẵn (máy không tải được gói NuGet)

Thư mục `bin/` và `obj/` **không** có trong git. Muốn chạy trên máy không tải được gói NuGet: build trên một máy có mạng (`dotnet build` trong `LuckyDraw\LuckyDraw.Web`), rồi chép **cả thư mục `LuckyDraw`** (gồm `LuckyDraw.Web\bin\Debug\net8.0` vừa build và `wwwroot`) sang máy đích bằng USB. Máy đích khi đó **không cần tải gói NuGet và không cần build**, nhưng vẫn cần:
- **ASP.NET Core Runtime 8** (hoặc .NET 8 SDK). Có thể tải bộ cài *offline* ở máy khác rồi chép sang bằng USB.
- **Mạng ra Internet tới Supabase**, vì database nằm trên mạng. Nếu hoàn toàn không có Internet, phải cài PostgreSQL ngay trên máy đó, chạy các script ở mục 2.2 bằng `psql`, và trỏ chuỗi kết nối về `Host=localhost`.

**Cách nhanh (Windows):** đặt chuỗi kết nối một lần, rồi nhấp đúp `LuckyDraw\chay-ban-build-san.bat`.

```bat
setx ConnectionStrings__LuckyDraw "Host=...;Port=5432;Database=postgres;Username=luckydraw_app.<project-ref>;Password=...;SSL Mode=Require;Search Path=luckydraw"
setx Seed__AdminPassword "..."   :: chỉ cần khi database chưa có tài khoản nào
```
`setx` lưu vĩnh viễn và chỉ có hiệu lực với cửa sổ mở **sau đó**. File `.bat` chạy ở cổng 5280. Muốn cổng khác thì chạy `chay-ban-build-san.bat 8080`.

**Hoặc gõ lệnh trực tiếp (PowerShell):**
```powershell
cd LuckyDraw\LuckyDraw.Web
$env:ConnectionStrings__LuckyDraw = "Host=...;Port=5432;Database=postgres;Username=...;Password=...;SSL Mode=Require;Search Path=luckydraw"
dotnet bin\Debug\net8.0\LuckyDraw.Web.dll --contentRoot (Get-Location).Path --urls http://0.0.0.0:5280
```

> ⚠️ `--contentRoot` phải là **đường dẫn tuyệt đối**. Nếu ghi `--contentRoot .`, app hiểu là thư mục chứa file `.dll` (`bin\Debug\net8.0`), không tìm thấy `wwwroot`, và màn chiếu mất toàn bộ CSS, JS, ảnh (lỗi 404).
>
> Chạy kiểu này là chế độ **Production**: User Secrets **không** được nạp, nên phải dùng biến môi trường như trên. Đừng dùng `dotnet run` trên máy khác: lệnh đó build lại (cần tải NuGet), và file `bin\...\staticwebassets.runtime.json` còn ghi đường dẫn của máy build.

Khi sửa code, nhớ build lại (`dotnet build`) rồi chép lại `bin` sang máy đích để bản chạy khớp với code.

Muốn app tự chạy cùng máy thì có thể dùng IIS (cài *ASP.NET Core Hosting Bundle*), Windows Service hoặc `systemd`.

Muốn bắt buộc đăng nhập mới xem được màn chiếu: đặt `"LuckyDraw": { "PublicView": false }` trong `appsettings.json`, hoặc biến môi trường `LuckyDraw__PublicView=false`.

---

## 9. Sao lưu

Phải sao lưu **cả hai** thứ:
1. **Database:** Supabase → *Database → Backups*, hoặc tự xuất bằng `pg_dump`.
2. **Thư mục `LuckyDraw.Web/wwwroot/uploads/`:** ảnh giải, ảnh chân dung, ảnh nền, nhạc. Thư mục này **chỉ nằm trên máy chạy app** và không có trong git. Chuyển app sang máy khác thì phải chép thư mục này theo.

---

## 10. Cập nhật lên bản mới

```bash
git pull
```
1. Xem trong `LuckyDraw/database/` có script mới không. Nếu có, chạy trong SQL Editor (các script nâng cấp như `04_…` chạy lại được nhiều lần). **Đừng chạy lại `01_schema.sql`**, vì nó xoá dữ liệu.
2. Dừng app (Ctrl+C) rồi `dotnet run` lại.
3. Trên trình duyệt nhấn **Ctrl + F5** để tải lại CSS/JS mới.

---

## 11. Xử lý sự cố

| Hiện tượng | Nguyên nhân và cách xử lý |
|---|---|
| `No such host is known` khi chạy | Chuỗi kết nối còn chữ mẫu (`aws-?-…`, `<project-ref>`). Thay bằng giá trị thật ở bước 2.4 |
| `password authentication failed` | Sai mật khẩu, hoặc thiếu `.<project-ref>` sau `luckydraw_app`. ⚠️ Sai nhiều lần sẽ bị chặn IP. Kiểm tra kỹ rồi mới thử lại |
| Kết nối được nhưng báo `relation … does not exist` | Thiếu `Search Path=luckydraw` trong chuỗi kết nối, hoặc chưa chạy `01_schema.sql` |
| `column … does not exist` (ví dụ `photo_path`, `position`) | Database cũ chưa nâng cấp. Chạy `04_participant_info.sql` |
| Supabase **Table Editor** không thấy bảng nào | Đang xem schema `public`. Đổi sang schema **`luckydraw`** |
| Không đăng nhập được lần đầu | Chưa đặt `Seed:AdminPassword` (bước 3), hoặc đang chạy bản publish mà quên biến `Seed__AdminPassword` |
| Màn chiếu báo lỗi JavaScript / thiếu nút sau khi cập nhật | App chưa khởi động lại sau khi `git pull`, hoặc trình duyệt còn giữ bản cũ. Khởi động lại app và nhấn **Ctrl + F5** |
| Không có tiếng | Trình duyệt chặn tự phát nhạc. Chạm vào màn hình một lần. Kiểm tra nút loa (phím **M**) |
| Máy khác không mở được | Chạy với `--urls http://0.0.0.0:5280` và mở cổng 5280 trong tường lửa (mục 5) |
| Giải báo "chỉ còn N người đủ điều kiện" | Không đủ người hợp lệ (đã trúng, vắng mặt, hoặc không đủ thâm niên). Xem trang Tổng quan |

### Sửa bản dịch giao diện
Câu gốc tiếng Việt nằm trong code, bản dịch nằm ở `LuckyDraw.Web/Resources/translations.tsv` (Tiếng Việt ⇥ English ⇥ 繁體 ⇥ 简体). Sửa file đó rồi chạy:
```bash
cd LuckyDraw
python tools/build_resx.py          # sinh lại file .resx
python tools/build_resx.py --check  # liệt kê câu chưa có bản dịch
```

---

Tài liệu kỹ thuật chi tiết (thiết kế database, API, bảo mật): [LuckyDraw/README.md](LuckyDraw/README.md) và [thiet-ke-luckydraw.md](thiet-ke-luckydraw.md).
