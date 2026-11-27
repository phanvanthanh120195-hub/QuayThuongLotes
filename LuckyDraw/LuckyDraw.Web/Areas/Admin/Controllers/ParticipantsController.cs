using System.IO.Compression;
using LuckyDraw.Web.Data;
using LuckyDraw.Web.Data.Repositories;
using LuckyDraw.Web.Models.Entities;
using LuckyDraw.Web.Models.ViewModels;
using LuckyDraw.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace LuckyDraw.Web.Areas.Admin.Controllers;

public class ParticipantsViewModel
{
    public Event Event { get; init; } = null!;
    public ParticipantSearch Search { get; init; } = new();
    public PagedList<ParticipantRow> Page { get; init; } = new();
    public ParticipantStats Stats { get; init; } = new();
    public IReadOnlyList<string> DepartmentNames { get; init; } = Array.Empty<string>();
}

public class ParticipantsController : AdminControllerBase
{
    /// <summary>Ảnh chân dung tối đa 5 MB mỗi ảnh; tải hàng loạt tối đa 300 MB mỗi lần.</summary>
    private const long MaxPhotoBytes = 5 * 1024 * 1024;
    private const long MaxBulkBytes = 300L * 1024 * 1024;
    private static readonly string[] PhotoExts = { ".jpg", ".jpeg", ".png", ".webp" };

    private readonly ParticipantRepository _participants;
    private readonly IFileStorage _files;

    public ParticipantsController(ParticipantRepository participants, IFileStorage files)
    {
        _participants = participants;
        _files = files;
    }

    public async Task<IActionResult> Index([FromQuery] ParticipantSearch search)
    {
        var ev = await WorkingEventAsync();
        if (ev is null) return NoEvent();
        if (search.Status is not (null or "" or "eligible" or "won" or "absent")) search.Status = null;
        return View(new ParticipantsViewModel
        {
            Event = ev,
            Search = search,
            Page = await _participants.SearchAsync(ev.EventId, search),
            Stats = await _participants.StatsAsync(ev.EventId),
            DepartmentNames = (await _participants.DepartmentNamesAsync(ev.EventId)).Vi
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (await WorkingEventAsync() is null) return NoEvent();
        await LoadDepartments();
        return View("Form", new ParticipantFormModel());
    }

    [HttpPost]
    public async Task<IActionResult> Create(ParticipantFormModel model)
    {
        var ev = await WorkingEventAsync();
        if (ev is null) return NoEvent();
        if (!ModelState.IsValid) return await FormView(model);

        var p = ToEntity(model, ev.EventId);
        try
        {
            var id = await _participants.InsertAsync(p);
            await Audit.LogAsync(AuditActions.ParticipantCreate, ev.EventId, "participant", id, new { p.EmployeeCode, p.FullName });
        }
        catch (Exception ex) when (PgErrors.Is(ex, PgErrors.UniqueViolation))
        {
            ModelState.AddModelError(nameof(model.EmployeeCode), L["Mã nhân viên đã có trong sự kiện này."]);
            return await FormView(model);
        }
        await Realtime.ConfigChanged(ev.EventId);
        Success(L["Đã thêm người tham dự."]);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var ev = await WorkingEventAsync();
        if (ev is null) return NoEvent();
        var p = await _participants.GetAsync(ev.EventId, id);
        if (p is null) return NotFound();
        return await FormView(ParticipantFormModel.From(p));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, ParticipantFormModel model)
    {
        var ev = await WorkingEventAsync();
        if (ev is null) return NoEvent();
        model.ParticipantId = id;
        var current = await _participants.GetAsync(ev.EventId, id);
        if (current is null) return NotFound();
        if (!ModelState.IsValid) return await FormView(model);

        var p = ToEntity(model, ev.EventId);
        // Form không còn ô Chức vụ → giữ nguyên giá trị đang có (vd. từ file import), không xoá mất
        p.Position = current.Position;
        try
        {
            if (!await _participants.UpdateAsync(p)) return NotFound();
        }
        catch (Exception ex) when (PgErrors.Is(ex, PgErrors.UniqueViolation))
        {
            ModelState.AddModelError(nameof(model.EmployeeCode), L["Mã nhân viên đã có trong sự kiện này."]);
            return await FormView(model);
        }
        await Audit.LogAsync(AuditActions.ParticipantUpdate, ev.EventId, "participant", id, new { p.EmployeeCode, p.FullName, p.IsAbsent });
        await Realtime.ConfigChanged(ev.EventId);
        Success(L["Đã lưu người tham dự."]);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleAbsent(int id, string? returnUrl)
    {
        var ev = await WorkingEventAsync();
        if (ev is null) return NoEvent();
        var p = await _participants.GetAsync(ev.EventId, id);
        if (p is null) return NotFound();
        await _participants.SetAbsentAsync(ev.EventId, id, !p.IsAbsent);
        await Audit.LogAsync(AuditActions.ParticipantAbsent, ev.EventId, "participant", id, new { isAbsent = !p.IsAbsent });
        await Realtime.ConfigChanged(ev.EventId);
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, string? returnUrl)
    {
        var ev = await WorkingEventAsync();
        if (ev is null) return NoEvent();
        try
        {
            var existing = await _participants.GetAsync(ev.EventId, id);
            if (existing != null && await _participants.DeleteAsync(ev.EventId, id))
            {
                await Audit.LogAsync(AuditActions.ParticipantDelete, ev.EventId, "participant", id);
                await Realtime.ConfigChanged(ev.EventId);
                Success(L["Đã xoá người tham dự."]);
            }
        }
        catch (Exception ex) when (PgErrors.Is(ex, PgErrors.ForeignKeyViolation))
        {
            Error(L["Người này đã có kết quả quay (kể cả đã xoá) nên không xoá được. Có thể đánh dấu vắng mặt."]);
        }
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction(nameof(Index));
    }

    /// <summary>Xoá tất cả người chưa từng có kết quả quay (dùng trước khi import lại danh sách).</summary>
    [HttpPost]
    public async Task<IActionResult> DeleteAll(string? confirm)
    {
        var ev = await WorkingEventAsync();
        if (ev is null) return NoEvent();
        if (!string.Equals(confirm?.Trim(), "XOA", StringComparison.OrdinalIgnoreCase))
        {
            Error(L["Gõ XOA để xác nhận."]);
            return RedirectToAction(nameof(Index));
        }
        var photos = await _participants.DeleteAllWithoutResultsAsync(ev.EventId);
        foreach (var photo in photos) _files.Delete(photo);
        var n = photos.Count;
        await Audit.LogAsync(AuditActions.ParticipantDeleteAll, ev.EventId, "participant", null, new { deleted = n });
        await Realtime.ConfigChanged(ev.EventId);
        Success(L["Đã xoá {0} người (giữ lại người đã có kết quả quay).", n]);
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Tải ảnh hàng loạt: chọn nhiều ảnh và/hoặc file .zip. Tên file = mã số thẻ (VN000032.jpg,
    /// "VN000032 - Tô Văn Thọ.jpg" cũng được). Ảnh trùng mã sẽ thay ảnh cũ.
    /// </summary>
    [HttpPost, RequestSizeLimit(MaxBulkBytes), RequestFormLimits(MultipartBodyLengthLimit = MaxBulkBytes)]
    public async Task<IActionResult> Photos(List<IFormFile> files)
    {
        var ev = await WorkingEventAsync();
        if (ev is null) return NoEvent();
        if (files.Count == 0)
        {
            Error(L["Chưa chọn ảnh."]);
            return RedirectToAction(nameof(Index));
        }

        var map = await _participants.PhotoMapAsync(ev.EventId);
        var done = new Dictionary<int, string>(); // participant_id → ảnh mới (ảnh sau cùng thắng)
        var unmatched = new List<string>();
        var invalid = new List<string>();

        async Task Handle(IFormFile file)
        {
            var hit = Match(map, Path.GetFileNameWithoutExtension(file.FileName));
            if (hit is null)
            {
                unmatched.Add(Path.GetFileName(file.FileName));
                return;
            }
            var up = await _files.SaveAsync(file, ev.EventId, "photo", UploadKind.Image, MaxPhotoBytes);
            if (!up.Ok)
            {
                invalid.Add($"{Path.GetFileName(file.FileName)}: {UploadError(up)}");
                return;
            }
            if (done.TryGetValue(hit.Value.Id, out var replaced)) _files.Delete(replaced);
            done[hit.Value.Id] = up.Path!;
        }

        foreach (var file in files.Where(f => f.Length > 0))
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext == ".zip")
            {
                try
                {
                    await using var zs = file.OpenReadStream();
                    using var zip = new ZipArchive(zs, ZipArchiveMode.Read);
                    foreach (var entry in zip.Entries.Take(5000))
                    {
                        var name = entry.FullName.Replace('\\', '/');
                        if (name.EndsWith('/') || name.Contains("__MACOSX/") || Path.GetFileName(name).StartsWith('.')) continue;
                        if (!PhotoExts.Contains(Path.GetExtension(name).ToLowerInvariant())) continue;
                        if (entry.Length > MaxPhotoBytes)
                        {
                            invalid.Add($"{Path.GetFileName(name)}: {L["File vượt quá {0} MB.", MaxPhotoBytes / 1024 / 1024]}");
                            continue;
                        }
                        var ms = new MemoryStream();
                        await using (var es = entry.Open()) await es.CopyToAsync(ms);
                        ms.Position = 0;
                        await Handle(new FormFile(ms, 0, ms.Length, "file", Path.GetFileName(name)));
                    }
                }
                catch (InvalidDataException)
                {
                    invalid.Add($"{file.FileName}: {L["Không đọc được file .zip."]}");
                }
            }
            else
            {
                await Handle(file);
            }
        }

        var oldPhotos = new List<string?>();
        foreach (var (pid, path) in done)
        {
            var old = map.Values.First(v => v.Id == pid).PhotoPath;
            if (await _participants.SetPhotoAsync(ev.EventId, pid, path)) oldPhotos.Add(old);
            else _files.Delete(path);
        }
        foreach (var old in oldPhotos) _files.Delete(old);

        await Audit.LogAsync(AuditActions.ParticipantPhotos, ev.EventId, "participant", null,
            new { updated = done.Count, unmatched = unmatched.Count, invalid = invalid.Count });
        if (done.Count > 0) await Realtime.ConfigChanged(ev.EventId);

        if (done.Count > 0) Success(L["Đã cập nhật ảnh cho {0} người.", done.Count]);
        var problems = unmatched.Select(u => L["{0}: không có mã số thẻ này", u].Value).Concat(invalid).ToList();
        if (problems.Count > 0)
        {
            Error(L["{0} ảnh không dùng được: {1}", problems.Count,
                string.Join("; ", problems.Take(8)) + (problems.Count > 8 ? "; …" : "")]);
        }
        else if (done.Count == 0)
        {
            Error(L["Không có ảnh nào hợp lệ."]);
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Tên file → người: khớp đúng mã, hoặc mã đứng đầu tên file rồi tới khoảng trắng / "_" / "-" / "(".</summary>
    private static (int Id, string? PhotoPath)? Match(Dictionary<string, (int Id, string? PhotoPath)> map, string stem)
    {
        stem = stem.Trim();
        // 1) Tên file đúng bằng mã: VN005621.jpg
        if (map.TryGetValue(stem, out var hit)) return hit;
        // 2) Mã đứng đầu, sau đó là phần chữ khác: "VN005621 - Phan Van Thanh", "VN005621-Phan", "VN005621_Phan"…
        //    Lấy đoạn chữ + số liền nhau ở đầu tên file (không phân biệt hoa thường).
        var n = 0;
        while (n < stem.Length && char.IsAsciiLetterOrDigit(stem[n])) n++;
        if (n > 0 && map.TryGetValue(stem[..n], out hit)) return hit;
        // 3) Mã có ký tự đặc biệt (ví dụ "NV-001"): cắt tại dấu phân cách đầu tiên
        foreach (var sep in new[] { " - ", " – ", " ", "_", "(", "." })
        {
            var i = stem.IndexOf(sep, StringComparison.Ordinal);
            if (i > 0 && map.TryGetValue(stem[..i].Trim(), out hit)) return hit;
        }
        return null;
    }

    /// <summary>Dữ liệu cho form: bộ phận + giải thưởng và ngày sự kiện (cột phải tính thâm niên / giải đủ điều kiện).</summary>
    private async Task LoadDepartments()
    {
        var ev = await WorkingEventAsync();
        var (vi, zh) = ev is null
            ? ((IReadOnlyList<string>)Array.Empty<string>(), (IReadOnlyList<string>)Array.Empty<string>())
            : await _participants.DepartmentNamesAsync(ev.EventId);
        ViewBag.DeptVi = vi;
        ViewBag.DeptZh = zh;
        ViewBag.Event = ev;
        ViewBag.Prizes = ev is null
            ? Array.Empty<Prize>()
            : await HttpContext.RequestServices.GetRequiredService<PrizeRepository>().ListByEventAsync(ev.EventId);
    }

    private async Task<IActionResult> FormView(ParticipantFormModel model)
    {
        await LoadDepartments();
        return View("Form", model);
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static Participant ToEntity(ParticipantFormModel m, int eventId) => new()
    {
        ParticipantId = m.ParticipantId,
        EventId = eventId,
        EmployeeCode = m.EmployeeCode.Trim(),
        FullName = m.FullName.Trim(),
        FullNameZh = Blank(m.FullNameZh),
        DepartmentVi = Blank(m.DepartmentVi),
        DepartmentZh = Blank(m.DepartmentZh),
        Position = Blank(m.Position),
        // Đường dẫn ảnh nhập tay (có thể dùng chung) nên không tự xoá file khi đổi / xoá người
        PhotoPath = ImagePathInput.Normalize(m.PhotoPath),
        DepartmentId = m.DepartmentId,
        JoinDate = m.JoinDate!.Value.Date,
        IsAbsent = m.IsAbsent
    };
}
