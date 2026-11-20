# QuayThuongLotes – LuckyDraw

Ứng dụng **quay thưởng tiệc tất niên**: trang Admin để chuẩn bị (sự kiện, giải thưởng, import file nhân sự, ảnh, giao diện, âm thanh) và màn chiếu quay số có hiệu ứng ô số cuộn và pháo hoa, hỗ trợ 4 ngôn ngữ (Tiếng Việt, 繁體中文, 简体中文, English).

ASP.NET Core 8 · PostgreSQL / Supabase · SignalR

- 📘 **[Hướng dẫn cài đặt và vận hành](HUONG-DAN-SETUP.md)**: bắt đầu từ đây
- 🛠️ [Tài liệu kỹ thuật](LuckyDraw/README.md): API, bảo mật, các trang Admin
- 📐 [Thiết kế chi tiết](thiet-ke-luckydraw.md)

Chạy nhanh (sau khi đã tạo database và đặt User Secrets theo hướng dẫn):

```bash
cd LuckyDraw/LuckyDraw.Web
dotnet run
# Màn chiếu: http://localhost:5280   ·   Admin: http://localhost:5280/admin
```

Pháo hoa trên màn chiếu chuyển thể từ *Firework Simulator* (Caleb Miller, giấy phép MIT).
