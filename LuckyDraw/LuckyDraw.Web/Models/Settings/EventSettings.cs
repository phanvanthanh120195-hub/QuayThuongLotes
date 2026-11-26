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
    public string TitleFont { get; set; } = "Playfair Display";
    public string TitleColor { get; set; } = "#FFD86B";
    public int TitleSize { get; set; } = 64;
    /// <summary>Hiện bảng hiệu đỏ viền vàng (2 đầu cuộn trục + mây) phía sau tên sự kiện.</summary>
    public bool TitleBanner { get; set; } = true;
    public LocalizedText CongratsTitle { get; set; } = new() { Vi = "Chúc mừng", ZhTW = "恭喜", ZhCN = "恭喜", En = "Congratulations" };
    public DigitBoxSettings DigitBox { get; set; } = new();
    public StartButtonSettings StartButton { get; set; } = new();
    public string ResultPanelPosition { get; set; } = "bottom";
    public string CongratsEffect { get; set; } = "confetti";
    /// <summary>Con trỏ chuột trên màn chiếu: gold | wand (đũa thần + tia pháo bông) | blossom | lantern | coin | rose | scepter (quyền trượng + hào quang) | dragon (rồng vờn ngọc) | system.</summary>
    public string Cursor { get; set; } = "gold";
    /// <summary>Cỡ con trỏ chuột (%): 100 = cỡ gốc (32px); hiệu ứng đi kèm (tia lửa, hào quang, rồng) to theo.</summary>
    public int CursorSize { get; set; } = 100;
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
    public int Style { get; set; } = 5;   // 5 = light gradient
    /// <summary>Pháo bông (tia lửa kiểu đũa thần) chạy vòng quanh viền nút.</summary>
    public bool Sparkler { get; set; } = true;
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
    /// <summary>
    /// Nhạc nền chung của cả sự kiện (vd. nhạc xuân) – phát suốt chương trình, lặp lại.
    /// Danh sách phát (single) hoặc nhạc riêng của giải (perPrize) được ưu tiên hơn nếu có.
    /// </summary>
    public SoundTrack? MainTrack { get; set; }
    /// <summary>Bật/tắt nhạc nền chung. Chưa tải bài nào thì dùng nhạc nền có sẵn (BuiltInSounds.DefaultMainPath).</summary>
    public bool MainEnabled { get; set; } = true;
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

/// <summary>
/// Mỗi thời điểm trỏ tới id một bài (thư viện sự kiện hoặc âm thanh có sẵn).
/// Chuỗi rỗng = admin chọn "Không"; thiếu trong JSON = dùng âm thanh có sẵn mặc định.
/// </summary>
public class SoundEffects
{
    /// <summary>Lặp trong lúc các ô đang quay (mặc định: nhạc Đột Kích).</summary>
    public string? Spin { get; set; } = BuiltInSounds.DotKich;
    /// <summary>Khi bấm Dừng (hoặc hết giờ quay tự động) – trước khi các cột chốt.</summary>
    public string? Stopping { get; set; } = BuiltInSounds.JackpotComing;
    /// <summary>Mỗi cột ô chốt số.</summary>
    public string? Land { get; set; } = BuiltInSounds.SpinTick;
    public string? Reveal { get; set; } = BuiltInSounds.JackpotWin;
    public string? Congrats { get; set; } = BuiltInSounds.CrowdCheer;
    public string? Click { get; set; } = BuiltInSounds.Bet;
}

/// <summary>Âm thanh đi kèm ứng dụng (wwwroot/mp3) – luôn có trong thư viện, không xoá được.</summary>
public static class BuiltInSounds
{
    public const string Prefix = "sys-";
    /// <summary>Nhạc nền chung có sẵn – phát khi bật nhạc nền mà chưa tải bài riêng.</summary>
    public const string DefaultMainPath = "/mp3/nhacnen.mp3";
    public const string Bet = "sys-bet";
    public const string RollerLoop = "sys-roller-loop";
    public const string DotKich = "sys-dot-kich";
    public const string RollerLoop2 = "sys-roller-loop-1";
    public const string SpinTick = "sys-spin";
    public const string SpinTick2 = "sys-spin-1";
    public const string JackpotComing = "sys-jackpot-coming";
    public const string JackpotWin = "sys-jackpot-win";
    public const string CrowdCheer = "sys-crowd-cheer";

    /// <summary>Tên hiển thị là khoá tiếng Việt – view dịch qua L[].</summary>
    public static readonly IReadOnlyList<SoundTrack> All = new SoundTrack[]
    {
        new() { Id = Bet, Name = "Bấm nút quay", Path = "/mp3/sm-bet.mp3" },
        new() { Id = "sys-quay-so", Name = "Quay số", Path = "/mp3/QuaySoP2.mp3" },
        new() { Id = RollerLoop, Name = "Vòng quay (lặp)", Path = "/mp3/sm-roller-loop.mp3" },
        new() { Id = DotKich, Name = "Nhạc Đột Kích", Path = "/mp3/nhac-dot-kich.mp3" },
        new() { Id = RollerLoop2, Name = "Vòng quay 2 (lặp)", Path = "/mp3/sm-roller-loop-1.mp3" },
        new() { Id = SpinTick, Name = "Tiếng chốt số", Path = "/mp3/sm-spin.mp3" },
        new() { Id = SpinTick2, Name = "Tiếng chốt số 2", Path = "/mp3/sm-spin-1.mp3" },
        new() { Id = JackpotComing, Name = "Hồi hộp chờ kết quả", Path = "/mp3/game-sm-jackpot-coming.mp3" },
        new() { Id = JackpotWin, Name = "Trúng giải", Path = "/mp3/game-sm-jackpot-win.mp3" },
        new() { Id = "sys-tada", Name = "Ta-da", Path = "/mp3/game-tada.mp3" },
        new() { Id = "sys-bonus", Name = "Thưởng thêm", Path = "/mp3/game-bonus.mp3" },
        new() { Id = "sys-levelup", Name = "Lên cấp", Path = "/mp3/game-levelup-s3.mp3" },
        new() { Id = CrowdCheer, Name = "Khán giả reo hò", Path = "/mp3/crowd-cheer-in-school.mp3" }
    };

    public static bool IsBuiltIn(string? id) => id != null && id.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>Thư viện sự kiện + âm thanh có sẵn (dùng cho hiệu ứng).</summary>
    public static IEnumerable<SoundTrack> WithLibrary(IEnumerable<SoundTrack> tracks) => tracks.Concat(All);
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
