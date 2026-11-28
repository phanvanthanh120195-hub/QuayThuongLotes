using LuckyDraw.Web.Areas.Admin.Controllers;
using LuckyDraw.Web.Data.Repositories;
using LuckyDraw.Web.Models.Dtos;
using LuckyDraw.Web.Models.Entities;
using LuckyDraw.Web.Models.Settings;
using LuckyDraw.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace LuckyDraw.Web.Controllers.Api;

/// <summary>
/// API chỉ đọc cho màn chiếu. Ai cũng xem được, trừ khi tắt "LuckyDraw:PublicView" thì phải đăng nhập.
/// </summary>
[ApiController]
[Route("api/view")]
public class ViewApiController : ControllerBase
{
    private readonly EventRepository _events;
    private readonly PrizeRepository _prizes;
    private readonly ParticipantRepository _participants;
    private readonly ResultRepository _results;
    private readonly IConfiguration _cfg;
    private readonly IStringLocalizer<SharedResource> L;

    public ViewApiController(EventRepository events, PrizeRepository prizes, ParticipantRepository participants,
        ResultRepository results, IConfiguration cfg, IStringLocalizer<SharedResource> localizer)
    {
        _events = events;
        _prizes = prizes;
        _participants = participants;
        _results = results;
        _cfg = cfg;
        L = localizer;
    }

    private bool Allowed => _cfg.GetValue("LuckyDraw:PublicView", true) || User.Identity?.IsAuthenticated == true;
    private bool CanDraw => User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Operator);

    [HttpGet("event")]
    public async Task<IActionResult> GetEvent()
    {
        if (!Allowed) return Unauthorized();
        var ev = await _events.GetActiveAsync();
        if (ev is null) return NotFound(new ApiError(L["Chưa có sự kiện nào đang chiếu"]));

        var theme = SettingsJson.Read<ThemeSettings>(ev.ThemeJson);
        var preset = ThemeOptions.Presets.FirstOrDefault(p => p.Key == theme.ThemeKey);
        if (preset.Key is null) preset = ThemeOptions.Presets[0];

        var (len, chars) = CodeShape(await _participants.CodesAsync(ev.EventId));
        return Ok(new EventViewDto(
            ev.EventId,
            LocalizedName.Get(ev),
            ev.EventDate.ToString("dd/MM/yyyy"),
            ev.Location,
            len,
            chars,
            theme,
            new ThemePresetDto(preset.Bg, preset.Accent, preset.Key == "minimal-light"),
            ToView(SettingsJson.Read<SoundSettings>(ev.SoundJson)),
            CanDraw,
            User.Identity?.Name,
            LocalizedName.Sub(ev)));
    }

    [HttpGet("prizes")]
    public async Task<IActionResult> GetPrizes()
    {
        if (!Allowed) return Unauthorized();
        var ev = await _events.GetActiveAsync();
        if (ev is null) return NotFound(new ApiError(L["Chưa có sự kiện nào đang chiếu"]));
        var list = (await _prizes.ListByEventAsync(ev.EventId))
            .Where(p => p.IsActive)
            .Select((p, i) =>
            {
                var remaining = Math.Max(0, p.Quantity - p.WonCount);
                return new PrizeViewDto(p.PrizeId, i + 1, LocalizedName.Get(p), p.ImagePath, p.Quantity, p.DrawPerTurn,
                    p.DrawType, p.SpinSeconds, p.StopSeconds, p.WonCount, remaining, p.EligibleCount,
                    Math.Min(p.DrawPerTurn, remaining), p.MinSeniorityMonths, p.MaxSeniorityMonths, LocalizedName.Reward(p));
            })
            .ToList();
        return Ok(list);
    }

    [HttpGet("results")]
    public async Task<IActionResult> GetResults(int? prizeId)
    {
        if (!Allowed) return Unauthorized();
        var ev = await _events.GetActiveAsync();
        if (ev is null) return NotFound(new ApiError(L["Chưa có sự kiện nào đang chiếu"]));
        var rows = await _results.ListAllActiveAsync(ev.EventId);
        return Ok(rows.Where(r => prizeId is null || r.PrizeId == prizeId)
            .OrderByDescending(r => r.DrawnAt).ThenByDescending(r => r.ResultId)
            .Select(ResultViewDto.From));
    }

    [HttpGet("results/last")]
    public async Task<IActionResult> GetLastBatch()
    {
        if (!Allowed) return Unauthorized();
        var ev = await _events.GetActiveAsync();
        if (ev is null) return NotFound(new ApiError(L["Chưa có sự kiện nào đang chiếu"]));
        var last = (await _results.RecentAsync(ev.EventId, 1)).FirstOrDefault();
        if (last is null) return Ok(Array.Empty<ResultViewDto>());
        return Ok((await _results.ListBatchAsync(last.BatchId)).Where(r => !r.IsRemoved).Select(ResultViewDto.From));
    }

    [HttpGet("participants")]
    public async Task<IActionResult> GetParticipants(string? q, int page = 1)
    {
        if (!Allowed) return Unauthorized();
        var ev = await _events.GetActiveAsync();
        if (ev is null) return NotFound(new ApiError(L["Chưa có sự kiện nào đang chiếu"]));
        var result = await _participants.SearchAsync(ev.EventId, new ParticipantSearch { Q = q, Page = page, PageSize = 50 });
        return Ok(new
        {
            items = result.Items.Select(p => new ParticipantViewDto(
                p.EmployeeCode, LocalizedName.Person(p), LocalizedName.Department(p),
                p.WonPrizeId != null ? "won" : p.IsAbsent ? "absent" : "waiting",
                LocalizedName.WonPrize(p), p.PhotoPath, p.Position)),
            total = result.Total,
            page = result.Page,
            totalPages = result.TotalPages
        });
    }

    /// <summary>
    /// Số ô quay = độ dài mã dài nhất (tối đa 12). Mỗi ô chỉ quay qua những ký tự thật sự xuất hiện ở vị trí đó
    /// (mã căn phải), nên tiền tố cố định như "NV" đứng yên, trông giống bảng số thật.
    /// </summary>
    public static (int Length, string[] Chars) CodeShape(IReadOnlyList<string> codes)
    {
        if (codes.Count == 0) return (4, Enumerable.Repeat("0123456789", 4).ToArray());
        var len = Math.Clamp(codes.Max(c => c.Length), 1, 12);
        var sets = Enumerable.Range(0, len).Select(_ => new SortedSet<char>()).ToArray();
        foreach (var code in codes)
        {
            var padded = code.Length >= len ? code[^len..] : code.PadLeft(len, ' ');
            for (var i = 0; i < len; i++) sets[i].Add(char.ToUpperInvariant(padded[i]));
        }
        return (len, sets.Select(s => new string(s.ToArray())).ToArray());
    }

    private static SoundViewDto ToView(SoundSettings s)
    {
        var paths = s.Tracks.ToDictionary(t => t.Id, t => t.Path);
        string? P(string? id) => id != null && paths.TryGetValue(id, out var p) ? p : null;
        // Hiệu ứng có thể trỏ tới âm thanh có sẵn (wwwroot/mp3)
        var fxPaths = BuiltInSounds.WithLibrary(s.Tracks).GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.First().Path);
        var fx = s.Effects;
        var effects = new Dictionary<string, string>();
        foreach (var (key, id) in new[]
                 {
                     ("spin", fx.Spin), ("stopping", fx.Stopping), ("land", fx.Land),
                     ("reveal", fx.Reveal), ("congrats", fx.Congrats), ("click", fx.Click)
                 })
        {
            if (id != null && fxPaths.TryGetValue(id, out var path)) effects[key] = path;
        }
        return new SoundViewDto(
            s.Mode, s.Volume, s.DuckOnSpin, s.PlaylistEnd,
            s.Playlist.Select(P).OfType<string>().ToList(),
            s.PrizeTracks.Where(kv => P(kv.Value) != null).ToDictionary(kv => kv.Key, kv => P(kv.Value)!),
            effects,
            s.MainEnabled ? s.MainTrack?.Path ?? BuiltInSounds.DefaultMainPath : null);   // tắt → không có nhạc nền chung
    }
}
