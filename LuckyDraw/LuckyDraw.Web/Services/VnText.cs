using System.Globalization;
using System.Text;

namespace LuckyDraw.Web.Services;

/// <summary>
/// Tìm kiếm tiếng Việt không dấu: "nguyen van a" khớp "Nguyễn Văn A", "dong" khớp "Đồng".
/// - <see cref="Fold"/>: bỏ dấu + chữ thường cho chuỗi tìm kiếm (C#).
/// - <see cref="Sql"/>: biểu thức SQL bỏ dấu + chữ thường cho một cột (dùng translate của PostgreSQL,
///   không cần cài extension unaccent).
/// </summary>
public static class VnText
{
    private const string Accented =
        "àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđ";

    // Bảng đổi ký tự có dấu → không dấu (cả chữ thường và chữ hoa), sinh tự động để hai chuỗi luôn dài bằng nhau
    private static readonly string From = Accented + Accented.ToUpperInvariant();
    private static readonly string To = new(From.Select(Plain).ToArray());

    private static char Plain(char c) => c switch
    {
        'đ' => 'd',
        'Đ' => 'D',
        _ => c.ToString().Normalize(NormalizationForm.FormD)[0]
    };

    /// <summary>Bỏ dấu, đổi đ → d, chữ thường. Ví dụ "Nguyễn Văn Đức" → "nguyen van duc".</summary>
    public static string Fold(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(ch == 'đ' ? 'd' : ch == 'Đ' ? 'D' : ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    /// <summary>Biểu thức SQL: cột đã bỏ dấu + chữ thường (NULL → ''). So khớp với <see cref="FoldPattern"/> bằng LIKE.</summary>
    public static string Sql(string column) =>
        $"lower(translate(normalize(coalesce({column}, ''), NFC), '{From}', '{To}'))";

    /// <summary>Mẫu LIKE '%…%' cho chuỗi tìm kiếm đã bỏ dấu ("" nếu không tìm).</summary>
    public static string FoldPattern(string? q) =>
        Data.Repositories.ParticipantRepository.LikePattern(Fold(q?.Trim()));
}
