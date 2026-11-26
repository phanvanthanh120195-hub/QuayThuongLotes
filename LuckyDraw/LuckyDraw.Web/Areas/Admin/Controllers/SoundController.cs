using LuckyDraw.Web.Data.Repositories;
using LuckyDraw.Web.Models.Entities;
using LuckyDraw.Web.Models.Settings;
using LuckyDraw.Web.Models.ViewModels;
using LuckyDraw.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace LuckyDraw.Web.Areas.Admin.Controllers;

public class SoundViewModel
{
    public Event Event { get; init; } = null!;
    public SoundSettings Sound { get; init; } = new();
    public IReadOnlyList<Prize> Prizes { get; init; } = Array.Empty<Prize>();
}

public class SoundController : AdminControllerBase
{
    private const long MaxAudioBytes = 15 * 1024 * 1024;
    private static readonly string[] Modes = { "none", "single", "perPrize" };

    private readonly EventRepository _events;
    private readonly PrizeRepository _prizes;
    private readonly IFileStorage _files;

    public SoundController(EventRepository events, PrizeRepository prizes, IFileStorage files)
    {
        _events = events;
        _prizes = prizes;
        _files = files;
    }

    public async Task<IActionResult> Index()
    {
        var ev = await WorkingEventAsync();
        if (ev is null) return NoEvent();
        return View(new SoundViewModel
        {
            Event = ev,
            Sound = SettingsJson.Read<SoundSettings>(ev.SoundJson),
            Prizes = await _prizes.ListByEventAsync(ev.EventId)
        });
    }

    [HttpPost]
    public async Task<IActionResult> Index(SoundFormModel model)
    {
        var ev = await WorkingEventAsync();
        if (ev is null) return NoEvent();
        var s = SettingsJson.Read<SoundSettings>(ev.SoundJson);
        var trackIds = s.Tracks.Select(t => t.Id).ToHashSet();
        string? Track(string? id) => id != null && trackIds.Contains(id) ? id : null;

        s.Mode = Modes.Contains(model.Mode) ? model.Mode : "single";
        s.Volume = Math.Clamp(model.Volume, 0, 100);
        s.DuckOnSpin = model.DuckOnSpin;
        s.PlaylistEnd = model.PlaylistEnd == "shuffle" ? "shuffle" : "repeat";
        // Danh sách phát giữ đúng thứ tự thư viện, chỉ lấy id có thật
        s.Playlist = s.Tracks.Where(t => model.Playlist.Contains(t.Id)).Select(t => t.Id).ToList();

        var prizeIds = (await _prizes.ListByEventAsync(ev.EventId)).Select(p => p.PrizeId.ToString()).ToHashSet();
        s.PrizeTracks = model.PrizeTracks
            .Where(kv => prizeIds.Contains(kv.Key) && Track(kv.Value) != null)
            .ToDictionary(kv => kv.Key, kv => kv.Value!);
        // Hiệu ứng được chọn cả âm thanh có sẵn; "" = admin chọn "Không" (khác với thiếu → mặc định)
        var effectIds = BuiltInSounds.WithLibrary(s.Tracks).Select(t => t.Id).ToHashSet();
        string Effect(string? id) => id != null && effectIds.Contains(id) ? id : "";
        s.Effects = new SoundEffects
        {
            Spin = Effect(model.Effects.Spin),
            Stopping = Effect(model.Effects.Stopping),
            Land = Effect(model.Effects.Land),
            Reveal = Effect(model.Effects.Reveal),
            Congrats = Effect(model.Effects.Congrats),
            Click = Effect(model.Effects.Click)
        };

        await Save(ev.EventId, s);
        Success(L["Đã lưu âm thanh."]);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [RequestSizeLimit(MaxAudioBytes + 64 * 1024)]
    public async Task<IActionResult> Upload(IFormFile? file, string? name)
    {
        var ev = await WorkingEventAsync();
        if (ev is null) return NoEvent();
        if (file is null || file.Length == 0)
        {
            Error(L["Hãy chọn file .mp3."]);
            return RedirectToAction(nameof(Index));
        }
        var up = await _files.SaveAsync(file, ev.EventId, "music", UploadKind.Audio, MaxAudioBytes);
        if (!up.Ok)
        {
            Error(UploadError(up));
            return RedirectToAction(nameof(Index));
        }

        var s = SettingsJson.Read<SoundSettings>(ev.SoundJson);
        var title = string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(file.FileName) : name.Trim();
        var track = new SoundTrack { Id = Guid.NewGuid().ToString("N")[..10], Name = title.Length > 80 ? title[..80] : title, Path = up.Path! };
        s.Tracks.Add(track);
        if (s.Mode == "single") s.Playlist.Add(track.Id);
        await Save(ev.EventId, s);
        Success(L["Đã thêm bài “{0}”.", track.Name]);
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Tải lên / thay nhạc nền chung của sự kiện.</summary>
    [HttpPost]
    [RequestSizeLimit(MaxAudioBytes + 64 * 1024)]
    public async Task<IActionResult> UploadMain(IFormFile? file)
    {
        var ev = await WorkingEventAsync();
        if (ev is null) return NoEvent();
        if (file is null || file.Length == 0)
        {
            Error(L["Hãy chọn file .mp3."]);
            return RedirectToAction(nameof(Index));
        }
        var up = await _files.SaveAsync(file, ev.EventId, "music", UploadKind.Audio, MaxAudioBytes);
        if (!up.Ok)
        {
            Error(UploadError(up));
            return RedirectToAction(nameof(Index));
        }

        var s = SettingsJson.Read<SoundSettings>(ev.SoundJson);
        var old = s.MainTrack;
        var title = Path.GetFileNameWithoutExtension(file.FileName);
        s.MainTrack = new SoundTrack { Id = "main", Name = title.Length > 80 ? title[..80] : title, Path = up.Path! };
        await Save(ev.EventId, s);
        if (old != null) _files.Delete(old.Path);
        Success(L["Đã đặt nhạc nền chung “{0}”.", s.MainTrack.Name]);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteMain()
    {
        var ev = await WorkingEventAsync();
        if (ev is null) return NoEvent();
        var s = SettingsJson.Read<SoundSettings>(ev.SoundJson);
        var old = s.MainTrack;
        if (old is null) return RedirectToAction(nameof(Index));

        s.MainTrack = null;
        await Save(ev.EventId, s);
        _files.Delete(old.Path);
        Success(L["Đã xoá nhạc nền chung."]);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteTrack(string id)
    {
        var ev = await WorkingEventAsync();
        if (ev is null) return NoEvent();
        var s = SettingsJson.Read<SoundSettings>(ev.SoundJson);
        var track = s.Tracks.FirstOrDefault(t => t.Id == id);
        if (track is null) return RedirectToAction(nameof(Index));

        s.Tracks.Remove(track);
        s.Playlist.Remove(id);
        foreach (var key in s.PrizeTracks.Where(kv => kv.Value == id).Select(kv => kv.Key).ToList()) s.PrizeTracks.Remove(key);
        var fx = s.Effects;
        if (fx.Spin == id) fx.Spin = "";
        if (fx.Stopping == id) fx.Stopping = "";
        if (fx.Land == id) fx.Land = "";
        if (fx.Reveal == id) fx.Reveal = "";
        if (fx.Congrats == id) fx.Congrats = "";
        if (fx.Click == id) fx.Click = "";
        await Save(ev.EventId, s);
        _files.Delete(track.Path);
        Success(L["Đã xoá bài “{0}”.", track.Name]);
        return RedirectToAction(nameof(Index));
    }

    private async Task Save(int eventId, SoundSettings s)
    {
        await _events.UpdateSoundAsync(eventId, SettingsJson.Write(s));
        await Audit.LogAsync(AuditActions.SoundUpdate, eventId, "event", eventId, new { s.Mode, tracks = s.Tracks.Count });
        await Realtime.ConfigChanged(eventId);
    }
}
