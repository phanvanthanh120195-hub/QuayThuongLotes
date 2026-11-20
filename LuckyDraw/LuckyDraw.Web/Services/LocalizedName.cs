using System.Globalization;
using LuckyDraw.Web.Models.Entities;
using LuckyDraw.Web.Models.Settings;

namespace LuckyDraw.Web.Services;

/// <summary>
/// Chọn tên theo ngôn ngữ đang dùng (mục 7.2 thiết kế):
///   zh-TW → zhTw ?? vi | zh-CN → zhCn ?? zhTw ?? vi | en → en ?? vi | khác → vi
/// </summary>
public static class LocalizedName
{
    public static string Get(string vi, string? zhTw, string? zhCn, string? en, CultureInfo? culture = null)
    {
        var name = (culture ?? CultureInfo.CurrentUICulture).Name;
        static string? N(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
        return name switch
        {
            "zh-TW" => N(zhTw) ?? vi,
            "zh-CN" => N(zhCn) ?? N(zhTw) ?? vi,
            _ when name.StartsWith("en") => N(en) ?? vi,
            _ => vi
        };
    }

    public static string Get(IMultiLangName x) => Get(x.NameVi, x.NameZhTw, x.NameZhCn, x.NameEn);

    public static string Get(LocalizedText? t) => t is null ? "" : Get(t.Vi ?? "", t.ZhTW, t.ZhCN, t.En);

    /// <summary>Tên người: zh-TW / zh-CN → 姓名 nếu có, còn lại → họ tên tiếng Việt.</summary>
    public static string Person(string fullName, string? fullNameZh, CultureInfo? culture = null)
    {
        var name = (culture ?? CultureInfo.CurrentUICulture).Name;
        return name.StartsWith("zh") && !string.IsNullOrWhiteSpace(fullNameZh) ? fullNameZh : fullName;
    }

    public static string Person(Participant p) => Person(p.FullName, p.FullNameZh);
    public static string Person(DrawResultRow r) => Person(r.FullName, r.FullNameZh);

    public static string? Department(ParticipantRow r)
        => r.DepartmentNameVi is null ? null : Get(r.DepartmentNameVi, r.DepartmentNameZhTw, r.DepartmentNameZhCn, r.DepartmentNameEn);

    public static string? WonPrize(ParticipantRow r)
        => r.WonPrizeNameVi is null ? null : Get(r.WonPrizeNameVi, r.WonPrizeNameZhTw, r.WonPrizeNameZhCn, r.WonPrizeNameEn);

    public static string? Department(DrawResultRow r)
        => r.DepartmentNameVi is null ? null : Get(r.DepartmentNameVi, r.DepartmentNameZhTw, r.DepartmentNameZhCn, r.DepartmentNameEn);

    public static string Prize(DrawResultRow r) => Get(r.PrizeNameVi, r.PrizeNameZhTw, r.PrizeNameZhCn, r.PrizeNameEn);
}

/// <summary>DB lưu UTC (TIMESTAMPTZ); hiển thị theo giờ Việt Nam.</summary>
public static class VnTime
{
    private static readonly TimeZoneInfo Zone = Find();

    private static TimeZoneInfo Find()
    {
        foreach (var id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch (TimeZoneNotFoundException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("ICT", TimeSpan.FromHours(7), "ICT", "ICT");
    }

    public static DateTime From(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    public static string Format(DateTime? utc, string format = "dd/MM/yyyy HH:mm")
        => utc is null ? "" : From(utc.Value).ToString(format, CultureInfo.InvariantCulture);
}

public static class Seniority
{
    /// <summary>Số tháng thâm niên tròn tính tới ngày sự kiện (khớp cách SQL so sánh ngày).</summary>
    public static int Months(DateTime joinDate, DateTime eventDate)
    {
        var months = (eventDate.Year - joinDate.Year) * 12 + eventDate.Month - joinDate.Month;
        // join_date <= event_date - n tháng  ⇔ đủ n tháng (AddMonths tự xử lý tháng thiếu ngày, như make_interval)
        while (months > 0 && joinDate.Date > eventDate.Date.AddMonths(-months)) months--;
        return Math.Max(0, months);
    }
}
