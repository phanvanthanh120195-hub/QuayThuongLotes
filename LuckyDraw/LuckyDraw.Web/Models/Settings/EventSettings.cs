using System.Text.Json;
using System.Text.Json.Serialization;

namespace LuckyDraw.Web.Models.Settings;

/// <summary>Chuỗi 4 ngôn ngữ dùng trong JSON cấu hình (theme_json).</summary>
public class LocalizedText
{
    public string? Vi { get; set; }
    public string? ZhTW { get; set; }
    public string? ZhCN { get; set; }
    public string? En { get; set; }
}

/// <summary>Cấu hình giao diện màn chiếu – lưu ở cột event.theme_json.</summary>
public class ThemeSettings
{
    public string ThemeKey { get; set; } = "tet-binh-ngo";
    public string? BackgroundPath { get; set; }
    public int BackgroundBlur { get; set; }
    public int BackgroundDim { get; set; } = 30;
    public string? LogoPath { get; set; }
    public string StageEffect { get; set; } = "fireworks";
    /// <summary>Hiệu ứng trong lúc đang quay số: fireworks (pháo hoa tia mảnh dày đặc) | none.</summary>
    public string SpinEffect { get; set; } = "fireworks";
    /// <summary>Tốc độ bay của pháo hoa (%): 100 = tốc độ thật, nhỏ hơn = chậm (kiểu quay chậm), lớn hơn = nhanh.</summary>
    public int FireworksSpeed { get; set; } = 100;
    public string TitleFont { get; set; } = "Anton";
    public string TitleColor { get; set; } = "#FFD86B";
    public int TitleSize { get; set; } = 64;
    public LocalizedText CongratsTitle { get; set; } = new() { Vi = "Chúc mừng", ZhTW = "恭喜", ZhCN = "恭喜", En = "Congratulations" };
    public DigitBoxSettings DigitBox { get; set; } = new();
    public StartButtonSettings StartButton { get; set; } = new();
    public string ResultPanelPosition { get; set; } = "bottom";
    public string CongratsEffect { get; set; } = "confetti";
}

public class DigitBoxSettings
{
    /// <summary>glass = ô kính đỏ viền sáng, gold = ô vàng</summary>
    public string Style { get; set; } = "glass";
    public int Radius { get; set; } = 12;
    public string Placeholder { get; set; } = "?";
    public string FontFamily { get; set; } = "Anton";
}

public class StartButtonSettings
{
    public int Style { get; set; } = 2;
    public LocalizedText Label { get; set; } = new() { Vi = "Bắt đầu", ZhTW = "開始", ZhCN = "开始", En = "Start" };
}

/// <summary>Cấu hình âm thanh – lưu ở cột event.sound_json.</summary>
public class SoundSettings
{
    /// <summary>none = không phát nhạc, single = 1 danh sách cho cả chương trình, perPrize = mỗi giải 1 bài</summary>
    public string Mode { get; set; } = "single";
    public int Volume { get; set; } = 70;
    public bool DuckOnSpin { get; set; } = true;
    /// <summary>repeat | shuffle khi hết danh sách</summary>
    public string PlaylistEnd { get; set; } = "repeat";
    public List<SoundTrack> Tracks { get; set; } = new();
    /// <summary>Id các bài trong danh sách phát (theo thứ tự).</summary>
    public List<string> Playlist { get; set; } = new();
    /// <summary>prizeId → trackId (chế độ mỗi giải 1 bài)</summary>
    public Dictionary<string, string> PrizeTracks { get; set; } = new();
    public SoundEffects Effects { get; set; } = new();
}

public class SoundTrack
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
}

public class SoundEffects
{
    public string? Spin { get; set; }
    public string? Reveal { get; set; }
    public string? Congrats { get; set; }
    public string? Click { get; set; }
}

public static class SettingsJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public static T Read<T>(string? json) where T : new()
    {
        if (string.IsNullOrWhiteSpace(json)) return new T();
        try { return JsonSerializer.Deserialize<T>(json, Options) ?? new T(); }
        catch (JsonException) { return new T(); }
    }

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
}
