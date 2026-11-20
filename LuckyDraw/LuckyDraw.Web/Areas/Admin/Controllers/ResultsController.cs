using ClosedXML.Excel;
using LuckyDraw.Web.Data;
using LuckyDraw.Web.Data.Repositories;
using LuckyDraw.Web.Models.Entities;
using LuckyDraw.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace LuckyDraw.Web.Areas.Admin.Controllers;

public class ResultsViewModel
{
    public Event Event { get; init; } = null!;
    public bool Removed { get; init; }
    public int? PrizeId { get; init; }
    public string? Q { get; init; }
    public PagedList<DrawResultRow> Page { get; init; } = new();
    public IReadOnlyList<Prize> Prizes { get; init; } = Array.Empty<Prize>();
}

public class ResultsController : AdminControllerBase
{
    private readonly ResultRepository _results;
    private readonly PrizeRepository _prizes;

    public ResultsController(ResultRepository results, PrizeRepository prizes)
    {
        _results = results;
        _prizes = prizes;
    }

    public async Task<IActionResult> Index(string? tab, int? prizeId, string? q, int page = 1)
    {
        var ev = await WorkingEventAsync();
        if (ev is null) return NoEvent();
        var removed = tab == "removed";
        return View(new ResultsViewModel
        {
            Event = ev,
            Removed = removed,
            PrizeId = prizeId,
            Q = q,
            Page = await _results.ListAsync(ev.EventId, removed, prizeId, q, page),
            Prizes = await _prizes.ListByEventAsync(ev.EventId)
        });
    }

    /// <summary>Admin xoá 1 kết quả (xoá mềm). Người đó được quay lại ở lượt sau.</summary>
    [HttpPost]
    public async Task<IActionResult> Delete(long id, string? returnUrl)
    {
        var ev = await WorkingEventAsync();
        if (ev is null) return NoEvent();
        var audit = Audit.Build(AuditActions.ResultDelete, ev.EventId, "draw_result", id);
        if (await _results.SoftDeleteAsync(ev.EventId, id, Audit.CurrentUser, audit))
        {
            await Realtime.ResultChanged(ev.EventId, null);
            Success(L["Đã xoá kết quả. Có thể khôi phục ở tab Kết quả đã xoá."]);
        }
        return Back(returnUrl);
    }

    [HttpPost("/admin/results/{id:long}/restore")]
    public async Task<IActionResult> Restore(long id, string? returnUrl)
    {
        var ev = await WorkingEventAsync();
        if (ev is null) return NoEvent();
        var audit = Audit.Build(AuditActions.ResultRestore, ev.EventId, "draw_result", id);
        var outcome = await _results.RestoreAsync(ev.EventId, id, audit);
        switch (outcome)
        {
            case RestoreOutcome.Restored:
                await Realtime.ResultChanged(ev.EventId, null);
                Success(L["Đã khôi phục kết quả."]);
                break;
            case RestoreOutcome.PrizeFull:
                Error(L["Giải này đã đủ người, không khôi phục được."]);
                break;
            case RestoreOutcome.AlreadyWon:
                Error(L["Người này đã trúng giải khác, không khôi phục được."]);
                break;
            case RestoreOutcome.NotRemoved:
                Error(L["Kết quả này đang có hiệu lực."]);
                break;
            default:
                return NotFound();
        }
        return Back(returnUrl);
    }

    /// <summary>Reset toàn bộ kết quả của sự kiện. Phải gõ lại đúng tên sự kiện để xác nhận.</summary>
    [HttpPost("/admin/results/reset")]
    public async Task<IActionResult> Reset(string? confirmName)
    {
        var ev = await WorkingEventAsync();
        if (ev is null) return NoEvent();
        if (!string.Equals(confirmName?.Trim(), ev.NameVi, StringComparison.Ordinal))
        {
            Error(L["Tên sự kiện nhập lại không khớp, chưa reset."]);
            return RedirectToAction(nameof(Index));
        }
        var audit = Audit.Build(AuditActions.ResultReset, ev.EventId, "event", ev.EventId);
        var n = await _results.ResetAllAsync(ev.EventId, Audit.CurrentUser, audit);
        await Realtime.ResultChanged(ev.EventId, null);
        Success(L["Đã reset {0} kết quả.", n]);
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Tải danh sách người trúng (.xlsx).</summary>
    [HttpGet]
    public async Task<IActionResult> Export()
    {
        var ev = await WorkingEventAsync();
        if (ev is null) return NoEvent();
        var rows = await _results.ListAllActiveAsync(ev.EventId);
        return File(ResultExporter.Build(ev, rows, L), ResultExporter.ContentType, ResultExporter.FileName(ev));
    }

    private IActionResult Back(string? returnUrl)
        => Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction(nameof(Index));
}
