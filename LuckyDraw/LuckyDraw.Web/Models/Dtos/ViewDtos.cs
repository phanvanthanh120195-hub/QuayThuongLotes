using LuckyDraw.Web.Models.Entities;
using LuckyDraw.Web.Models.Settings;
using LuckyDraw.Web.Services;

namespace LuckyDraw.Web.Models.Dtos;

/// <summary>Dữ liệu màn chiếu nhận từ /api/view/event. Tên đã chọn sẵn theo ngôn ngữ người xem.</summary>
public record EventViewDto(
    int EventId,
    string Name,
    string Date,
    string? Location,
    int CodeLength,
    string[] CodeChars,
    ThemeSettings Theme,
    ThemePresetDto Preset,
    SoundViewDto Sound,
    bool CanDraw,
    string? UserName);

public record ThemePresetDto(string Bg, string Accent, bool Light);

public record SoundViewDto(
    string Mode,
    int Volume,
    bool DuckOnSpin,
    string PlaylistEnd,
    IReadOnlyList<string> Playlist,
    IReadOnlyDictionary<string, string> PrizeTracks,
    IReadOnlyDictionary<string, string> Effects,
    // Nhạc nền chung – phát khi không có danh sách phát / nhạc riêng của giải
    string? Main);

public record PrizeViewDto(
    int PrizeId,
    int Order,
    string Name,
    string? ImagePath,
    int Quantity,
    int DrawPerTurn,
    int DrawType,
    int SpinSeconds,
    int StopSeconds,
    int Won,
    int Remaining,
    int Eligible,
    int NextTake,
    int? MinSeniorityMonths,
    int? MaxSeniorityMonths);

public record ResultViewDto(
    long ResultId,
    int PrizeId,
    string PrizeName,
    string Code,
    string Name,
    string? Department,
    string DrawnAt,
    Guid BatchId,
    string? Photo,
    string? Position)
{
    public static ResultViewDto From(DrawResultRow r) => new(
        r.ResultId, r.PrizeId, LocalizedName.Prize(r), r.EmployeeCode, LocalizedName.Person(r), LocalizedName.Department(r),
        VnTime.Format(r.DrawnAt, "HH:mm:ss"), r.BatchId, r.PhotoPath, r.Position);
}

public record ParticipantViewDto(string Code, string Name, string? Department, string Status, string? Prize, string? Photo, string? Position);

public record DrawRequest(int PrizeId);

public record DrawResponse(Guid BatchId, IReadOnlyList<ResultViewDto> Winners, int Won, int Quantity);

public record ApiError(string Message);
