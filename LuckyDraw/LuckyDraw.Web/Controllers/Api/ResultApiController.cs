using LuckyDraw.Web.Data.Repositories;
using LuckyDraw.Web.Models.Dtos;
using LuckyDraw.Web.Models.Entities;
using LuckyDraw.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace LuckyDraw.Web.Controllers.Api;

/// <summary>POST /api/results/{id}/absent – "Vắng mặt – quay lại" trên trang chính (mục 6 thiết kế).</summary>
[ApiController]
[Route("api/results")]
[Authorize(Roles = Roles.AdminOrOperator)]
public class ResultApiController : ControllerBase
{
    private readonly ResultRepository _results;
    private readonly EventRepository _events;
    private readonly AuditService _audit;
    private readonly RealtimeNotifier _realtime;
    private readonly IStringLocalizer<SharedResource> L;

    public ResultApiController(ResultRepository results, EventRepository events, AuditService audit,
        RealtimeNotifier realtime, IStringLocalizer<SharedResource> localizer)
    {
        _results = results;
        _events = events;
        _audit = audit;
        _realtime = realtime;
        L = localizer;
    }

    /// <summary>Nút "Tải kết quả" trên màn chiếu: danh sách người trúng của sự kiện đang chiếu (.xlsx).</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export()
    {
        var ev = await _events.GetActiveAsync();
        if (ev is null) return NotFound(new ApiError(L["Chưa có sự kiện nào đang chiếu"]));
        var rows = await _results.ListAllActiveAsync(ev.EventId);
        return File(ResultExporter.Build(ev, rows, L), ResultExporter.ContentType, ResultExporter.FileName(ev));
    }

    [HttpPost("{id:long}/absent")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAbsent(long id)
    {
        var ev = await _events.GetActiveAsync();
        if (ev is null) return NotFound(new ApiError(L["Chưa có sự kiện nào đang chiếu"]));

        var prizeId = await _results.MarkAbsentAsync(ev.EventId, id, _audit.CurrentUser,
            _audit.Build(AuditActions.MarkAbsent, ev.EventId, "draw_result", id));
        if (prizeId is null) return Conflict(new ApiError(L["Kết quả này đã được xử lý rồi."]));

        await _realtime.ResultChanged(ev.EventId, prizeId);
        return Ok(new { prizeId });
    }
}
