using System.ComponentModel.DataAnnotations;
using LuckyDraw.Web.Models.Entities;
using LuckyDraw.Web.Models.Settings;
using Microsoft.Extensions.Localization;

namespace LuckyDraw.Web.Models.ViewModels;

/// <summary>Câu báo lỗi dùng chung (cũng là khoá đa ngôn ngữ).</summary>
public static class Msg
{
    public const string Required = "Vui lòng nhập {0}.";
    public const string MaxLen = "{0} tối đa {1} ký tự.";
    public const string Range = "{0} phải từ {1} đến {2}.";
    public const string Code = "{0} chỉ gồm chữ, số và ký tự . _ -";
    public const string ImagePath = "{0} phải tính từ thư mục wwwroot (vd: /images/prizes/tivi.png) hoặc là URL http(s), không dùng đường dẫn ổ đĩa như C:\\...";
}

/// <summary>Đường dẫn ảnh nhập tay (ảnh giải thưởng, ảnh chân dung).</summary>
public static class ImagePathInput
{
    public const int MaxLength = 300;

    /// <summary>
    /// Bỏ khoảng trắng, đổi "\" thành "/", bỏ tiền tố "~" / "wwwroot", thêm "/" ở đầu (tính từ wwwroot).
    /// URL http(s) giữ nguyên. Rỗng → null (không có ảnh). Phía trình duyệt: admin.js (data-image-path) làm y hệt.
    /// </summary>
    public static string? Normalize(string? s)
    {
        s = s?.Trim().Replace('\\', '/');
        if (string.IsNullOrEmpty(s)) return null;
        if (IsHttp(s)) return s;
        if (s.StartsWith("~/")) s = s[1..];
        if (s.StartsWith("/wwwroot/", StringComparison.OrdinalIgnoreCase)) s = s["/wwwroot".Length..];
        else if (s.StartsWith("wwwroot/", StringComparison.OrdinalIgnoreCase)) s = s["wwwroot".Length..];
        return s.StartsWith('/') ? s : "/" + s;
    }

    /// <summary>Hợp lệ: rỗng, URL http(s), hoặc đường dẫn web không chứa ":" (chặn C:\..., javascript:, data:...).</summary>
    public static bool IsValid(string? s)
    {
        var n = Normalize(s);
        return n is null || (n.Length <= MaxLength && (IsHttp(n) || !n.Contains(':')));
    }

    private static bool IsHttp(string s) =>
        s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
}

public sealed class ImagePathAttribute : ValidationAttribute
{
    public ImagePathAttribute() => ErrorMessage = Msg.ImagePath;
    public override bool IsValid(object? value) => ImagePathInput.IsValid(value as string);
}

public class LoginModel
{
    [Required(ErrorMessage = Msg.Required), Display(Name = "Tên đăng nhập")]
    public string UserName { get; set; } = "";

    [Required(ErrorMessage = Msg.Required), DataType(DataType.Password), Display(Name = "Mật khẩu")]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

public abstract class MultiLangForm
{
    [Required(ErrorMessage = Msg.Required), StringLength(200, ErrorMessage = Msg.MaxLen), Display(Name = "Tên (Tiếng Việt)")]
    public string NameVi { get; set; } = "";

    [StringLength(200, ErrorMessage = Msg.MaxLen), Display(Name = "Tên (繁體中文)")]
    public string? NameZhTw { get; set; }

    [StringLength(200, ErrorMessage = Msg.MaxLen), Display(Name = "Tên (简体中文)")]
    public string? NameZhCn { get; set; }

    [StringLength(200, ErrorMessage = Msg.MaxLen), Display(Name = "Tên (English)")]
    public string? NameEn { get; set; }
}

public class EventFormModel : MultiLangForm
{
    public int EventId { get; set; }

    [Required(ErrorMessage = Msg.Required), DataType(DataType.Date), Display(Name = "Ngày diễn ra")]
    public DateTime? EventDate { get; set; }

    [StringLength(200, ErrorMessage = Msg.MaxLen), Display(Name = "Địa điểm")]
    public string? Location { get; set; }

    public bool IsActive { get; set; }
    public bool HasResults { get; set; }

    public static EventFormModel From(Event e) => new()
    {
        EventId = e.EventId,
        NameVi = e.NameVi,
        NameZhTw = e.NameZhTw,
        NameZhCn = e.NameZhCn,
        NameEn = e.NameEn,
        EventDate = e.EventDate,
        Location = e.Location,
        IsActive = e.IsActive
    };
}

public class DepartmentFormModel : MultiLangForm
{
    public int DepartmentId { get; set; }

    [Required(ErrorMessage = Msg.Required), StringLength(30, ErrorMessage = Msg.MaxLen),
     RegularExpression(@"^[A-Za-z0-9_.\-]+$", ErrorMessage = Msg.Code), Display(Name = "Mã bộ phận")]
    public string Code { get; set; } = "";

    [Range(0, 9999, ErrorMessage = Msg.Range), Display(Name = "Thứ tự")]
    public int SortOrder { get; set; }

    public static DepartmentFormModel From(Department d) => new()
    {
        DepartmentId = d.DepartmentId,
        Code = d.Code,
        NameVi = d.NameVi,
        NameZhTw = d.NameZhTw,
        NameZhCn = d.NameZhCn,
        NameEn = d.NameEn,
        SortOrder = d.SortOrder
    };
}

public class PrizeFormModel : MultiLangForm, IValidatableObject
{
    public int PrizeId { get; set; }

    [Range(1, 10000, ErrorMessage = Msg.Range), Display(Name = "Số người trúng tối đa")]
    public int Quantity { get; set; } = 1;

    [Range(1, 500, ErrorMessage = Msg.Range), Display(Name = "Số kết quả mỗi lượt quay")]
    public int DrawPerTurn { get; set; } = 1;

    [Range(1, 2, ErrorMessage = Msg.Range), Display(Name = "Kiểu quay")]
    public short DrawType { get; set; } = 1;

    [Range(1, 120, ErrorMessage = Msg.Range), Display(Name = "Thời gian quay (giây)")]
    public int SpinSeconds { get; set; } = 5;

    [Range(0, 60, ErrorMessage = Msg.Range), Display(Name = "Thời gian dừng (giây)")]
    public int StopSeconds { get; set; } = 2;

    [Range(0, 600, ErrorMessage = Msg.Range), Display(Name = "Thâm niên tối thiểu (tháng)")]
    public int? MinSeniorityMonths { get; set; }

    [Range(1, 600, ErrorMessage = Msg.Range), Display(Name = "Thâm niên tối đa (tháng)")]
    public int? MaxSeniorityMonths { get; set; }

    [Display(Name = "Đang dùng")]
    public bool IsActive { get; set; } = true;

    [StringLength(ImagePathInput.MaxLength, ErrorMessage = Msg.MaxLen), ImagePath, Display(Name = "Đường dẫn ảnh")]
    public string? ImagePath { get; set; }

    [StringLength(300, ErrorMessage = Msg.MaxLen), Display(Name = "Phần thưởng (Tiếng Việt)")]
    public string? RewardVi { get; set; }

    [StringLength(300, ErrorMessage = Msg.MaxLen), Display(Name = "Phần thưởng (繁體中文)")]
    public string? RewardZhTw { get; set; }

    [StringLength(300, ErrorMessage = Msg.MaxLen), Display(Name = "Phần thưởng (简体中文)")]
    public string? RewardZhCn { get; set; }

    [StringLength(300, ErrorMessage = Msg.MaxLen), Display(Name = "Phần thưởng (English)")]
    public string? RewardEn { get; set; }

    public int WonCount { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var L = validationContext.GetService(typeof(IStringLocalizer<SharedResource>)) as IStringLocalizer<SharedResource>;
        string T(string s) => L?[s] ?? s;

        if (DrawPerTurn > Quantity)
            yield return new ValidationResult(T("Số người mỗi lượt không được lớn hơn số lượng giải"), new[] { nameof(DrawPerTurn) });
        if (MinSeniorityMonths.HasValue && MaxSeniorityMonths.HasValue && MaxSeniorityMonths <= MinSeniorityMonths)
            yield return new ValidationResult(T("Thâm niên tối đa phải lớn hơn tối thiểu"), new[] { nameof(MaxSeniorityMonths) });
        if (WonCount > Quantity)
            yield return new ValidationResult(T("Số lượng không được nhỏ hơn số người đã trúng"), new[] { nameof(Quantity) });
    }

    public static PrizeFormModel From(Prize p) => new()
    {
        PrizeId = p.PrizeId,
        NameVi = p.NameVi,
        NameZhTw = p.NameZhTw,
        NameZhCn = p.NameZhCn,
        NameEn = p.NameEn,
        Quantity = p.Quantity,
        DrawPerTurn = p.DrawPerTurn,
        DrawType = p.DrawType,
        SpinSeconds = p.SpinSeconds,
        StopSeconds = p.StopSeconds,
        MinSeniorityMonths = p.MinSeniorityMonths,
        MaxSeniorityMonths = p.MaxSeniorityMonths,
        IsActive = p.IsActive,
        ImagePath = p.ImagePath,
        RewardVi = p.RewardVi,
        RewardZhTw = p.RewardZhTw,
        RewardZhCn = p.RewardZhCn,
        RewardEn = p.RewardEn,
        WonCount = p.WonCount
    };
}

public class ParticipantFormModel
{
    public int ParticipantId { get; set; }

    [Required(ErrorMessage = Msg.Required), StringLength(30, ErrorMessage = Msg.MaxLen), Display(Name = "Mã nhân viên")]
    public string EmployeeCode { get; set; } = "";

    [Required(ErrorMessage = Msg.Required), StringLength(200, ErrorMessage = Msg.MaxLen), Display(Name = "Họ tên")]
    public string FullName { get; set; } = "";

    [StringLength(200, ErrorMessage = Msg.MaxLen), Display(Name = "Tên tiếng Trung (姓名)")]
    public string? FullNameZh { get; set; }

    [StringLength(200, ErrorMessage = Msg.MaxLen), Display(Name = "Bộ phận")]
    public string? DepartmentVi { get; set; }

    [StringLength(200, ErrorMessage = Msg.MaxLen), Display(Name = "Bộ phận tiếng Trung (現任單位)")]
    public string? DepartmentZh { get; set; }

    [StringLength(200, ErrorMessage = Msg.MaxLen), Display(Name = "Chức vụ")]
    public string? Position { get; set; }

    [StringLength(ImagePathInput.MaxLength, ErrorMessage = Msg.MaxLen), ImagePath, Display(Name = "Đường dẫn ảnh")]
    public string? PhotoPath { get; set; }

    /// <summary>Danh mục bộ phận cũ – ẩn, chỉ giữ nguyên giá trị khi sửa.</summary>
    public int? DepartmentId { get; set; }

    [Required(ErrorMessage = Msg.Required), DataType(DataType.Date), Display(Name = "Ngày vào làm")]
    public DateTime? JoinDate { get; set; }

    [Display(Name = "Vắng mặt")]
    public bool IsAbsent { get; set; }

    public static ParticipantFormModel From(Participant p) => new()
    {
        ParticipantId = p.ParticipantId,
        EmployeeCode = p.EmployeeCode,
        FullName = p.FullName,
        FullNameZh = p.FullNameZh,
        DepartmentVi = p.DepartmentVi,
        DepartmentZh = p.DepartmentZh,
        Position = p.Position,
        PhotoPath = p.PhotoPath,
        DepartmentId = p.DepartmentId,
        JoinDate = p.JoinDate,
        IsAbsent = p.IsAbsent
    };
}

public class UserCreateModel
{
    [Required(ErrorMessage = Msg.Required), StringLength(50, MinimumLength = 3, ErrorMessage = "{0} dài từ {2} đến {1} ký tự."),
     RegularExpression(@"^[A-Za-z0-9_.\-]+$", ErrorMessage = Msg.Code), Display(Name = "Tên đăng nhập")]
    public string UserName { get; set; } = "";

    [StringLength(100, ErrorMessage = Msg.MaxLen), Display(Name = "Tên hiển thị")]
    public string? DisplayName { get; set; }

    [Required(ErrorMessage = Msg.Required), Display(Name = "Vai trò")]
    public string Role { get; set; } = Roles.Operator;

    [Required(ErrorMessage = Msg.Required), StringLength(100, MinimumLength = 8, ErrorMessage = "{0} dài từ {2} đến {1} ký tự."),
     DataType(DataType.Password), Display(Name = "Mật khẩu")]
    public string Password { get; set; } = "";

    [Compare(nameof(Password), ErrorMessage = "Mật khẩu nhập lại không khớp."), DataType(DataType.Password),
     Display(Name = "Nhập lại mật khẩu")]
    public string ConfirmPassword { get; set; } = "";
}

public class UserEditModel
{
    public int UserId { get; set; }
    public string UserName { get; set; } = "";

    [StringLength(100, ErrorMessage = Msg.MaxLen), Display(Name = "Tên hiển thị")]
    public string? DisplayName { get; set; }

    [Required(ErrorMessage = Msg.Required), Display(Name = "Vai trò")]
    public string Role { get; set; } = Roles.Operator;

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;
}

public class UserPasswordModel
{
    public int UserId { get; set; }
    public string UserName { get; set; } = "";

    [Required(ErrorMessage = Msg.Required), StringLength(100, MinimumLength = 8, ErrorMessage = "{0} dài từ {2} đến {1} ký tự."),
     DataType(DataType.Password), Display(Name = "Mật khẩu mới")]
    public string Password { get; set; } = "";

    [Compare(nameof(Password), ErrorMessage = "Mật khẩu nhập lại không khớp."), DataType(DataType.Password),
     Display(Name = "Nhập lại mật khẩu")]
    public string ConfirmPassword { get; set; } = "";
}

public class ThemeFormModel
{
    public ThemeSettings Theme { get; set; } = new();
    public IFormFile? Background { get; set; }
    public bool RemoveBackground { get; set; }
    public IFormFile? Logo { get; set; }
    public bool RemoveLogo { get; set; }
}

public class SoundFormModel
{
    public string Mode { get; set; } = "single";

    [Range(0, 100)]
    public int Volume { get; set; } = 70;

    public bool DuckOnSpin { get; set; }
    /// <summary>Bật nhạc nền chung (bỏ chọn → false).</summary>
    public bool MainEnabled { get; set; }
    public string PlaylistEnd { get; set; } = "repeat";
    public List<string> Playlist { get; set; } = new();
    public Dictionary<string, string?> PrizeTracks { get; set; } = new();
    public SoundEffects Effects { get; set; } = new();
}
