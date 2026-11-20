using LuckyDraw.Web.Data.Repositories;
using LuckyDraw.Web.Models.Entities;
using Microsoft.AspNetCore.Identity;

namespace LuckyDraw.Web.Services;

/// <summary>
/// Lúc khởi động: nếu bảng app_user còn trống thì tạo tài khoản Admin đầu tiên.
/// Mật khẩu đọc từ cấu hình "Seed:AdminPassword" (User Secrets), không viết cứng trong code.
///   dotnet user-secrets set "Seed:AdminPassword" "mật-khẩu-mạnh"
/// </summary>
public class AdminSeeder : IHostedService
{
    private readonly IServiceProvider _sp;
    private readonly IConfiguration _cfg;
    private readonly ILogger<AdminSeeder> _log;

    public AdminSeeder(IServiceProvider sp, IConfiguration cfg, ILogger<AdminSeeder> log)
    {
        _sp = sp;
        _cfg = cfg;
        _log = log;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _sp.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserRepository>();
            if (await users.CountAsync() > 0) return;

            var userName = _cfg["Seed:AdminUserName"] ?? "admin";
            var password = _cfg["Seed:AdminPassword"];
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            {
                _log.LogWarning("Chưa có tài khoản nào. Đặt 'Seed:AdminPassword' (>= 8 ký tự) trong User Secrets rồi chạy lại để tạo tài khoản admin.");
                return;
            }

            var user = new AppUser { UserName = userName, DisplayName = "Administrator", Role = Roles.Admin, IsActive = true };
            user.PasswordHash = new PasswordHasher<AppUser>().HashPassword(user, password);
            await users.InsertAsync(user);
            _log.LogInformation("Đã tạo tài khoản admin đầu tiên: {User}", userName);
        }
        catch (Exception ex)
        {
            // Không chặn app khởi động khi DB lỗi: trang sẽ báo lỗi kết nối khi truy cập
            _log.LogError(ex, "Không tạo được tài khoản admin (kiểm tra chuỗi kết nối DB).");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
