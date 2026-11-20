using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace LuckyDraw.Web.Helpers;

public static class HtmlExtensions
{
    /// <summary>Icon SVG từ sprite trong layout: @Html.Icon("gift")</summary>
    public static IHtmlContent Icon(this IHtmlHelper _, string name, string? css = null)
        => new HtmlString($"<svg class=\"ic {HtmlEncoder.Default.Encode(css ?? "")}\" aria-hidden=\"true\"><use href=\"#i-{HtmlEncoder.Default.Encode(name)}\"/></svg>");
}

/// <summary>Dữ liệu cho partial _Pager.</summary>
public class PagerModel
{
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public int Total { get; init; }
    public string Action { get; init; } = "Index";
    public Dictionary<string, string?> Route { get; init; } = new();
}
