using LuckyDraw.Web.Data.Repositories;
using LuckyDraw.Web.Models.Settings;
using LuckyDraw.Web.Models.ViewModels;
using LuckyDraw.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace LuckyDraw.Web.Areas.Admin.Controllers;

public static class ThemeOptions
{
    public static readonly (string Key, string Label, string Bg, string Accent)[] Presets =
    {
        ("tet-binh-ngo", "Tết Bính Ngọ 2026", "url('/images/themes/tet-dinh-mui.jpg') center / cover no-repeat, #8b0000", "#FFD86B"),
        ("tet-red-gold", "Tết đỏ vàng", "radial-gradient(circle at 50% 30%, #b3122a, #4a0410 75%)", "#FFD86B"),
        ("royal-blue", "Xanh hoàng gia", "radial-gradient(circle at 50% 30%, #1f4fbf, #08143d 75%)", "#F5D27A"),
        ("galaxy", "Dải ngân hà", "radial-gradient(circle at 50% 20%, #5b2a86, #0b0820 75%)", "#9BE7FF"),
        ("emerald", "Ngọc lục bảo", "radial-gradient(circle at 50% 30%, #0f7a5a, #022a1f 75%)", "#FFE29A"),
        ("minimal-light", "Sáng tối giản", "linear-gradient(160deg, #ffffff, #e9ecf3)", "#C8102E")

    };

    public static readonly (string Key, string Label)[] StageEffects =
    {
        ("none", "Không"), ("fireworks", "Pháo hoa"), ("confetti", "Giấy màu"), ("snow", "Tuyết rơi"), ("sparkles", "Lấp lánh")
    };

    public static readonly (string Key, string Label)[] SpinEffects =
    {
        ("none", "Không"), ("fireworks", "Pháo hoa")
    };

    public static readonly (string Key, string Label)[] CongratsEffects =
    {
        ("none", "Không"), ("confetti", "Giấy màu"), ("fireworks", "Pháo hoa")
    };

    public static readonly string[] Fonts = { "Playfair Display", "Noto Serif Display", "Be Vietnam Pro", "Montserrat", "Lobster", "Paytone One", "Anton", "Bebas Neue", "Oswald" };

    public static readonly (string Key, string Label)[] DigitStyles =
    {
        ("glass", "Kính đỏ"), ("gold", "Vàng")
    };

    /// <summary>Con trỏ chuột màn chiếu. Ảnh: /images/cursor/{Key}.svg và {Key}-hover.svg (rồng: .png); HotX/HotY = điểm bấm
    /// (phải khớp bảng CURSORS trong wwwroot/js/draw.js).</summary>
    public static readonly (string Key, string Label, int HotX, int HotY)[] Cursors =
    {
        ("gold", "Mũi tên vàng", 3, 2), ("wand", "Đũa thần pháo bông", 5, 5), ("blossom", "Hoa mai", 16, 16),
        ("lantern", "Đèn lồng", 16, 1), ("coin", "Đồng tiền vàng", 16, 16), ("rose", "Mũi tên hồng", 3, 2),
        ("scepter", "Quyền trượng hào quang", 15, 15), ("dragon", "Rồng vờn ngọc", 20, 20),
        ("system", "Con trỏ hệ thống", 0, 0)
    };

    /// <summary>Tên file ảnh con trỏ (trong /images/cursor).</summary>
    public static string CursorFile(string key, bool hover = false) =>
        key + (hover ? "-hover" : "") + (key == "dragon" ? ".png" : ".svg");

    public static readonly (string Key, string Label)[] ResultPositions =
    {
        ("bottom", "Dưới ô quay số"), ("left", "Bên trái"), ("right", "Bên phải")
    };
}

public class ThemeController : AdminControllerBase
{
    private readonly EventRepository _events;
    private readonly IFileStorage _files;

    public ThemeController(EventRepository events, IFileStorage files)
    {
        _events = events;
        _files = files;
    }

    public async Task<IActionResult> Index()
    {
        var ev = await WorkingEventAsync();
        if (ev is null) return NoEvent();
        ViewBag.Event = ev;
        return View(new ThemeFormModel { Theme = SettingsJson.Read<ThemeSettings>(ev.ThemeJson) });
    }

    [HttpPost]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> Index(ThemeFormModel model)
    {
        var ev = await WorkingEventAsync();
        if (ev is null) return NoEvent();
        var old = SettingsJson.Read<ThemeSettings>(ev.ThemeJson);
        var t = Sanitize(model.Theme);

        // Đường dẫn file lấy từ bản đang lưu, không nhận từ form
        t.BackgroundPath = model.RemoveBackground ? null : old.BackgroundPath;
        t.LogoPath = model.RemoveLogo ? null : old.LogoPath;

        if (model.Background is { Length: > 0 })
        {
            var up = await _files.SaveAsync(model.Background, ev.EventId, "bg", UploadKind.Image, 5 * 1024 * 1024);
            if (up.Ok) t.BackgroundPath = up.Path;
            else ModelState.AddModelError(nameof(model.Background), UploadError(up));
        }
        if (model.Logo is { Length: > 0 })
        {
            var up = await _files.SaveAsync(model.Logo, ev.EventId, "logo", UploadKind.Image, 2 * 1024 * 1024);
            if (up.Ok) t.LogoPath = up.Path;
            else ModelState.AddModelError(nameof(model.Logo), UploadError(up));
        }
        if (!ModelState.IsValid)
        {
            // Có lỗi thì không lưu gì: xoá file vừa upload thành công, giữ ảnh cũ
            if (t.BackgroundPath != old.BackgroundPath && t.BackgroundPath != null) _files.Delete(t.BackgroundPath);
            if (t.LogoPath != old.LogoPath && t.LogoPath != null) _files.Delete(t.LogoPath);
            t.BackgroundPath = old.BackgroundPath;
            t.LogoPath = old.LogoPath;
            ViewBag.Event = ev;
            return View(new ThemeFormModel { Theme = t });
        }

        await _events.UpdateThemeAsync(ev.EventId, SettingsJson.Write(t));
        if (old.BackgroundPath != t.BackgroundPath) _files.Delete(old.BackgroundPath);
        if (old.LogoPath != t.LogoPath) _files.Delete(old.LogoPath);
        await Audit.LogAsync(AuditActions.ThemeUpdate, ev.EventId, "event", ev.EventId, new { t.ThemeKey, t.StageEffect });
        await Realtime.ConfigChanged(ev.EventId);
        Success(L["Đã lưu giao diện."]);
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Chỉ nhận giá trị trong danh sách cho phép; số kẹp trong khoảng hợp lệ.</summary>
    private static ThemeSettings Sanitize(ThemeSettings t)
    {
        string Pick(string? v, IEnumerable<string> allowed, string fallback) => allowed.Contains(v) ? v! : fallback;
        static string Color(string? c, string fallback)
            => c is { Length: 7 } && c[0] == '#' && c.Skip(1).All(Uri.IsHexDigit) ? c.ToUpperInvariant() : fallback;
        static LocalizedText Text(LocalizedText? x, int max) => new()
        {
            Vi = Cut(x?.Vi, max), ZhTW = Cut(x?.ZhTW, max), ZhCN = Cut(x?.ZhCN, max), En = Cut(x?.En, max)
        };

        var d = new ThemeSettings();
        return new ThemeSettings
        {
            ThemeKey = Pick(t.ThemeKey, ThemeOptions.Presets.Select(p => p.Key), d.ThemeKey),
            BackgroundBlur = Math.Clamp(t.BackgroundBlur, 0, 20),
            BackgroundDim = Math.Clamp(t.BackgroundDim, 0, 80),
            StageEffect = Pick(t.StageEffect, ThemeOptions.StageEffects.Select(p => p.Key), d.StageEffect),
            SpinEffect = Pick(t.SpinEffect, ThemeOptions.SpinEffects.Select(p => p.Key), d.SpinEffect),
            FireworksSpeed = t.FireworksSpeed <= 0 ? d.FireworksSpeed : Math.Clamp(t.FireworksSpeed, 30, 200),
            TitleFont = Pick(t.TitleFont, ThemeOptions.Fonts, d.TitleFont),
            TitleColor = Color(t.TitleColor, d.TitleColor),
            TitleSize = Math.Clamp(t.TitleSize, 24, 140),
            CongratsTitle = Text(t.CongratsTitle, 60),
            DigitBox = new DigitBoxSettings
            {
                Style = Pick(t.DigitBox?.Style, ThemeOptions.DigitStyles.Select(s => s.Key), "glass"),
                Radius = Math.Clamp(t.DigitBox?.Radius ?? 30, 0, 50),
                Placeholder = string.IsNullOrEmpty(t.DigitBox?.Placeholder) ? "?" : t.DigitBox.Placeholder[..1],
                FontFamily = Pick(t.DigitBox?.FontFamily, ThemeOptions.Fonts, "Anton")
            },
            StartButton = new StartButtonSettings
            {
                Style = Math.Clamp(t.StartButton?.Style ?? 5, 1, 6),
                Label = Text(t.StartButton?.Label, 30)
            },
            ResultPanelPosition = Pick(t.ResultPanelPosition, ThemeOptions.ResultPositions.Select(p => p.Key), "bottom"),
            CongratsEffect = Pick(t.CongratsEffect, ThemeOptions.CongratsEffects.Select(p => p.Key), "confetti"),
            Cursor = Pick(t.Cursor, ThemeOptions.Cursors.Select(c => c.Key), d.Cursor),
            CursorSize = t.CursorSize <= 0 ? d.CursorSize : Math.Clamp(t.CursorSize, 80, 250)
        };
    }

    private static string? Cut(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        return s.Length <= max ? s : s[..max];
    }
}
