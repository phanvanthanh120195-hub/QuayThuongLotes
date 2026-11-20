using LuckyDraw.Web.Data;
using LuckyDraw.Web.Data.Repositories;
using LuckyDraw.Web.Models.Entities;
using LuckyDraw.Web.Models.ViewModels;
using LuckyDraw.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace LuckyDraw.Web.Areas.Admin.Controllers;

public class PrizesController : AdminControllerBase
{
    private const long MaxImageBytes = 2 * 1024 * 1024;

    private readonly PrizeRepository _prizes;
    private readonly IFileStorage _files;

    public PrizesController(PrizeRepository prizes, IFileStorage files)
    {
        _prizes = prizes;
        _files = files;
    }

    public async Task<IActionResult> Index()
    {
        var ev = await WorkingEventAsync();
        if (ev is null) return NoEvent();
        ViewBag.Event = ev;
        return View(await _prizes.ListByEventAsync(ev.EventId));
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (await WorkingEventAsync() is null) return NoEvent();
        return View("Form", new PrizeFormModel());
    }

    [HttpPost]
    public async Task<IActionResult> Create(PrizeFormModel model)
    {
        var ev = await WorkingEventAsync();
        if (ev is null) return NoEvent();
        if (!ModelState.IsValid) return View("Form", model);

        var prize = new Prize { EventId = ev.EventId };
        Apply(model, prize);
        if (model.Image is { Length: > 0 })
        {
            var up = await _files.SaveAsync(model.Image, ev.EventId, "prize", UploadKind.Image, MaxImageBytes);
            if (!up.Ok)
            {
                ModelState.AddModelError(nameof(model.Image), UploadError(up));
                return View("Form", model);
            }
            prize.ImagePath = up.Path;
        }

        var id = await _prizes.InsertAsync(prize);
        await Audit.LogAsync(AuditActions.PrizeCreate, ev.EventId, "prize", id, new { prize.NameVi, prize.Quantity });
        await Realtime.ConfigChanged(ev.EventId);
        Success(L["Đã thêm giải thưởng."]);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var ev = await WorkingEventAsync();
        if (ev is null) return NoEvent();
        var p = await _prizes.GetAsync(ev.EventId, id);
        return p is null ? NotFound() : View("Form", PrizeFormModel.From(p));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, PrizeFormModel model)
    {
        var ev = await WorkingEventAsync();
        if (ev is null) return NoEvent();
        var prize = await _prizes.GetAsync(ev.EventId, id);
        if (prize is null) return NotFound();

        // Số đã trúng lấy từ DB, không tin giá trị form gửi lên
        model.PrizeId = id;
        model.WonCount = prize.WonCount;
        model.ImagePath = prize.ImagePath;
        ModelState.Clear();
        TryValidateModel(model);
        if (!ModelState.IsValid) return View("Form", model);

        var oldImage = prize.ImagePath;
        Apply(model, prize);
        if (model.Image is { Length: > 0 })
        {
            var up = await _files.SaveAsync(model.Image, ev.EventId, "prize", UploadKind.Image, MaxImageBytes);
            if (!up.Ok)
            {
                ModelState.AddModelError(nameof(model.Image), UploadError(up));
                return View("Form", model);
            }
            prize.ImagePath = up.Path;
        }
        else if (model.RemoveImage)
        {
            prize.ImagePath = null;
        }

        await _prizes.UpdateAsync(prize);
        if (oldImage != prize.ImagePath) _files.Delete(oldImage);
        await Audit.LogAsync(AuditActions.PrizeUpdate, ev.EventId, "prize", id, new { prize.NameVi, prize.Quantity, prize.DrawPerTurn });
        await Realtime.ConfigChanged(ev.EventId);
        Success(L["Đã lưu giải thưởng."]);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Move(int id, int direction)
    {
        var ev = await WorkingEventAsync();
        if (ev is null) return NoEvent();
        var ids = (await _prizes.ListByEventAsync(ev.EventId)).Select(p => p.PrizeId).ToList();
        var i = ids.IndexOf(id);
        var j = i + (direction < 0 ? -1 : 1);
        if (i >= 0 && j >= 0 && j < ids.Count)
        {
            (ids[i], ids[j]) = (ids[j], ids[i]);
            await _prizes.ReorderAsync(ev.EventId, ids);
            await Audit.LogAsync(AuditActions.PrizeReorder, ev.EventId, "prize", id, new { order = ids });
            await Realtime.ConfigChanged(ev.EventId);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var ev = await WorkingEventAsync();
        if (ev is null) return NoEvent();
        var p = await _prizes.GetAsync(ev.EventId, id);
        if (p is null) return NotFound();
        await _prizes.SetActiveAsync(ev.EventId, id, !p.IsActive);
        await Audit.LogAsync(AuditActions.PrizeUpdate, ev.EventId, "prize", id, new { isActive = !p.IsActive });
        await Realtime.ConfigChanged(ev.EventId);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await WorkingEventAsync();
        if (ev is null) return NoEvent();
        var p = await _prizes.GetAsync(ev.EventId, id);
        if (p is null) return NotFound();
        try
        {
            await _prizes.DeleteAsync(ev.EventId, id);
        }
        catch (Exception ex) when (PgErrors.Is(ex, PgErrors.ForeignKeyViolation))
        {
            Error(L["Giải đã có kết quả quay (kể cả đã xoá) nên không xoá được. Hãy tắt giải thay vì xoá."]);
            return RedirectToAction(nameof(Index));
        }
        _files.Delete(p.ImagePath);
        await Audit.LogAsync(AuditActions.PrizeDelete, ev.EventId, "prize", id, new { p.NameVi });
        await Realtime.ConfigChanged(ev.EventId);
        Success(L["Đã xoá giải thưởng."]);
        return RedirectToAction(nameof(Index));
    }

    private static void Apply(PrizeFormModel m, Prize p)
    {
        p.NameVi = m.NameVi.Trim();
        p.NameZhTw = string.IsNullOrWhiteSpace(m.NameZhTw) ? null : m.NameZhTw.Trim();
        p.NameZhCn = string.IsNullOrWhiteSpace(m.NameZhCn) ? null : m.NameZhCn.Trim();
        p.NameEn = string.IsNullOrWhiteSpace(m.NameEn) ? null : m.NameEn.Trim();
        p.Quantity = m.Quantity;
        p.DrawPerTurn = m.DrawPerTurn;
        p.DrawType = m.DrawType;
        p.SpinSeconds = m.SpinSeconds;
        p.StopSeconds = m.StopSeconds;
        p.MinSeniorityMonths = m.MinSeniorityMonths;
        p.MaxSeniorityMonths = m.MaxSeniorityMonths;
        p.IsActive = m.IsActive;
    }
}
