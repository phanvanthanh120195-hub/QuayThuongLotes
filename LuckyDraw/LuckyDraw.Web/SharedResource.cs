namespace LuckyDraw.Web;

/// <summary>
/// Lớp rỗng làm "khoá" cho file Resources/SharedResource.{culture}.resx.
/// Khoá của mỗi chuỗi chính là câu tiếng Việt, nên tiếng Việt không cần file .resx:
/// không tìm thấy bản dịch thì IStringLocalizer trả về chính khoá.
/// </summary>
public class SharedResource
{
}

public static class SupportedCultures
{
    public const string Default = "vi-VN";
    public static readonly string[] All = { "vi-VN", "zh-TW", "zh-CN", "en-US" };

    public static readonly (string Code, string Label)[] Options =
    {
        ("vi-VN", "Tiếng Việt"),
        ("zh-TW", "繁體中文"),
        ("zh-CN", "简体中文"),
        ("en-US", "English")
    };
}
