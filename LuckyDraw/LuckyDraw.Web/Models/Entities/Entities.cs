namespace LuckyDraw.Web.Models.Entities;

/// <summary>Các bảng có tên 4 ngôn ngữ.</summary>
public interface IMultiLangName
{
    string NameVi { get; }
    string? NameZhTw { get; }
    string? NameZhCn { get; }
    string? NameEn { get; }
}

public class Event : IMultiLangName
{
    public int EventId { get; set; }
    public string NameVi { get; set; } = "";
    public string? NameZhTw { get; set; }
    public string? NameZhCn { get; set; }
    public string? NameEn { get; set; }
    public DateTime EventDate { get; set; }
    public string? Location { get; set; }
    public bool IsActive { get; set; }
    public string ThemeJson { get; set; } = "{}";
    public string SoundJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Sự kiện kèm số liệu (cho trang danh sách).</summary>
public class EventSummary : Event
{
    public int PrizeCount { get; set; }
    public int ParticipantCount { get; set; }
    public int WinnerCount { get; set; }
    public int TotalQuantity { get; set; }
}

public class Department : IMultiLangName
{
    public int DepartmentId { get; set; }
    public string Code { get; set; } = "";
    public string NameVi { get; set; } = "";
    public string? NameZhTw { get; set; }
    public string? NameZhCn { get; set; }
    public string? NameEn { get; set; }
    public int SortOrder { get; set; }
    public int ParticipantCount { get; set; }
}

public class Prize : IMultiLangName
{
    public int PrizeId { get; set; }
    public int EventId { get; set; }
    public int SortOrder { get; set; }
    public string NameVi { get; set; } = "";
    public string? NameZhTw { get; set; }
    public string? NameZhCn { get; set; }
    public string? NameEn { get; set; }
    public int Quantity { get; set; }
    public int DrawPerTurn { get; set; } = 1;
    /// <summary>1 = Thủ công, 2 = Tự động</summary>
    public short DrawType { get; set; } = 1;
    public int SpinSeconds { get; set; } = 5;
    public int StopSeconds { get; set; } = 2;
    public int? MinSeniorityMonths { get; set; }
    public int? MaxSeniorityMonths { get; set; }
    public string? ImagePath { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Phần thưởng của giải (vd. "20 triệu tiền mặt"), 4 ngôn ngữ; để trống = không hiện.</summary>
    public string? RewardVi { get; set; }
    public string? RewardZhTw { get; set; }
    public string? RewardZhCn { get; set; }
    public string? RewardEn { get; set; }

    /// <summary>Số kết quả còn hiệu lực (không lưu trong bảng, lấy bằng COUNT).</summary>
    public int WonCount { get; set; }
    /// <summary>Số người còn đủ điều kiện quay giải này (tính khi cần).</summary>
    public int EligibleCount { get; set; }
}

public class Participant
{
    public int ParticipantId { get; set; }
    public int EventId { get; set; }
    public string EmployeeCode { get; set; } = "";
    public string FullName { get; set; } = "";
    /// <summary>姓名 – tên tiếng Trung (hiện khi màn chiếu dùng zh-TW / zh-CN).</summary>
    public string? FullNameZh { get; set; }
    /// <summary>Bộ phận / 現任單位 ghi thẳng theo file nhân sự.</summary>
    public string? DepartmentVi { get; set; }
    public string? DepartmentZh { get; set; }
    /// <summary>Chức vụ.</summary>
    public string? Position { get; set; }
    /// <summary>Ảnh chân dung (đường dẫn /uploads/...).</summary>
    public string? PhotoPath { get; set; }
    /// <summary>Danh mục bộ phận cũ (đang ẩn khỏi Admin, vẫn giữ để tương thích).</summary>
    public int? DepartmentId { get; set; }
    public DateTime JoinDate { get; set; }
    public bool IsAbsent { get; set; }
}

/// <summary>Người tham dự kèm bộ phận và giải đã trúng (cho trang danh sách).</summary>
public class ParticipantRow : Participant
{
    public string? DepartmentCode { get; set; }
    public string? DepartmentNameVi { get; set; }
    public string? DepartmentNameZhTw { get; set; }
    public string? DepartmentNameZhCn { get; set; }
    public string? DepartmentNameEn { get; set; }
    public int? WonPrizeId { get; set; }
    public string? WonPrizeNameVi { get; set; }
    public string? WonPrizeNameZhTw { get; set; }
    public string? WonPrizeNameZhCn { get; set; }
    public string? WonPrizeNameEn { get; set; }
    /// <summary>Tổng số dòng khớp bộ lọc (count(*) OVER ()), dùng cho phân trang.</summary>
    public long TotalCount { get; set; }
}

public class DrawResultRow
{
    public long ResultId { get; set; }
    public int EventId { get; set; }
    public int PrizeId { get; set; }
    public int ParticipantId { get; set; }
    public Guid BatchId { get; set; }
    public DateTime DrawnAt { get; set; }
    public string DrawnBy { get; set; } = "";
    public bool IsRemoved { get; set; }
    public short? RemovedReason { get; set; }
    public DateTime? RemovedAt { get; set; }
    public string? RemovedBy { get; set; }

    public string EmployeeCode { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? FullNameZh { get; set; }
    public string? Position { get; set; }
    public string? PhotoPath { get; set; }
    public string? DepartmentNameVi { get; set; }
    public string? DepartmentNameZhTw { get; set; }
    public string? DepartmentNameZhCn { get; set; }
    public string? DepartmentNameEn { get; set; }
    public string PrizeNameVi { get; set; } = "";
    public string? PrizeNameZhTw { get; set; }
    public string? PrizeNameZhCn { get; set; }
    public string? PrizeNameEn { get; set; }
    public int PrizeSortOrder { get; set; }
    public long TotalCount { get; set; }
}

public static class RemovedReasons
{
    public const short Absent = 1;
    public const short AdminDeleted = 2;
    public const short Reset = 3;
}

public class AppUser
{
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string? DisplayName { get; set; }
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = Roles.Operator;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string Operator = "Operator";
    public const string AdminOrOperator = "Admin,Operator";
}

public class AuditLog
{
    public long AuditId { get; set; }
    public int? EventId { get; set; }
    public string Action { get; set; } = "";
    public string? Entity { get; set; }
    public string? EntityId { get; set; }
    public string? Detail { get; set; }
    public string UserName { get; set; } = "";
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PagedList<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int Total { get; init; }
    public int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}
