using LuckyDraw.Web.Data;
using LuckyDraw.Web.Data.Repositories;
using LuckyDraw.Web.Models.Entities;
using LuckyDraw.Web.Models.Settings;
using LuckyDraw.Web.Models.ViewModels;
using LuckyDraw.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace LuckyDraw.Web.Areas.Admin.Controllers;

public class EventsController : AdminControllerBase
{
    private readonly EventRepository _events;
    public EventsController(EventRepository events) => _events = events;

    public async Task<IActionResult> Index()
    {
        ViewBag.WorkingEventId = (await WorkingEventAsync())?.EventId;
        return View(await _events.ListAsync());
    }

    [HttpGet]
    public IActionResult Create() => View("Form", new EventFormModel { EventDate = DateTime.Today.AddDays(14) });

    [HttpPost]
    public async Task<IActionResult> Create(EventFormModel model)
    {
        if (!ModelState.IsValid) return View("Form", model);

        var ev = new Event
        {
            NameVi = model.NameVi.Trim(),
            NameZhTw = Clean(model.NameZhTw),
            NameZhCn = Clean(model.NameZhCn),
            NameEn = Clean(model.NameEn),
            EventDate = model.EventDate!.Value.Date,
            Location = Clean(model.Location),
            ThemeJson = SettingsJson.Write(new ThemeSettings()),
            SoundJson = SettingsJson.Write(new SoundSettings())
        };
        var id = await _events.InsertAsync(ev);
        await Audit.LogAsync(AuditActions.EventCreate, id, "event", id, new { ev.NameVi, ev.EventDate });
        EventContext.Set(id);
        Success(L["Đã tạo sự kiện. Tiếp theo: thêm giải thưởng và người tham dự."]);
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var ev = await _events.GetAsync(id);
        if (ev is null) return NotFound();
        var model = EventFormModel.From(ev);
        model.HasResults = await _events.CountResultsAsync(id) > 0;
        return View("Form", model);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, EventFormModel model)
    {
        var current = await _events.GetAsync(id);
        if (current is null) return NotFound();
        if (!ModelState.IsValid)
        {
            model.EventId = id;
            model.IsActive = current.IsActive;
            return View("Form", model);
        }

        current.NameVi = model.NameVi.Trim();
        current.NameZhTw = Clean(model.NameZhTw);
        current.NameZhCn = Clean(model.NameZhCn);
        current.NameEn = Clean(model.NameEn);
        current.EventDate = model.EventDate!.Value.Date;
        current.Location = Clean(model.Location);
        await _events.UpdateAsync(current);
        await Audit.LogAsync(AuditActions.EventUpdate, id, "event", id, new { current.NameVi, current.EventDate });
        await Realtime.ConfigChanged(id);
        Success(L["Đã lưu sự kiện."]);
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Đặt làm sự kiện đang chiếu trên màn hình chính.</summary>
    [HttpPost]
    public async Task<IActionResult> Activate(int id)
    {
        if (!await _events.SetActiveAsync(id)) return NotFound();
        await Audit.LogAsync(AuditActions.EventActivate, id, "event", id);
        await Realtime.ActiveEventChanged();
        Success(L["Màn hình chính đang chiếu sự kiện này."]);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Deactivate(int id)
    {
        if (await _events.DeactivateAsync(id))
        {
            await Audit.LogAsync(AuditActions.EventDeactivate, id, "event", id);
            await Realtime.ActiveEventChanged();
            Success(L["Đã ngừng chiếu sự kiện."]);
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Chọn sự kiện để quản lý trong Admin (không ảnh hưởng màn chiếu).</summary>
    [HttpPost]
    public async Task<IActionResult> Use(int id, string? returnUrl)
    {
        if (await _events.GetAsync(id) is null) return NotFound();
        EventContext.Set(id);
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, string? confirmName)
    {
        var ev = await _events.GetAsync(id);
        if (ev is null) return NotFound();
        if (ev.IsActive)
        {
            Error(L["Không xoá được sự kiện đang chiếu. Hãy ngừng chiếu trước."]);
            return RedirectToAction(nameof(Index));
        }
        if (!string.Equals(confirmName?.Trim(), ev.NameVi, StringComparison.Ordinal))
        {
            Error(L["Tên sự kiện nhập lại không khớp."]);
            return RedirectToAction(nameof(Index));
        }
        try
        {
            await _events.DeleteAsync(id);
        }
        catch (Exception ex) when (PgErrors.Is(ex, PgErrors.ForeignKeyViolation))
        {
            Error(L["Sự kiện đã có kết quả quay nên không xoá được (giữ lại để đối chiếu)."]);
            return RedirectToAction(nameof(Index));
        }
        await Audit.LogAsync(AuditActions.EventDelete, id, "event", id, new { ev.NameVi });
        Success(L["Đã xoá sự kiện."]);
        return RedirectToAction(nameof(Index));
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
