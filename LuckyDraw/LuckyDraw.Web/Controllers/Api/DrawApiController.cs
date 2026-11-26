using LuckyDraw.Web.Models.Dtos;
using LuckyDraw.Web.Models.Entities;
using LuckyDraw.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace LuckyDraw.Web.Controllers.Api;

/// <summary>POST /api/draw – quay 1 lượt. Chỉ MC (Operator) và Admin; chống CSRF bằng header RequestVerificationToken.</summary>
[ApiController]
[Route("api/draw")]
[Authorize(Roles = Roles.AdminOrOperator)]
public class DrawApiController : ControllerBase
{
    private readonly DrawService _draw;
    private readonly IStringLocalizer<SharedResource> L;
    private readonly ILogger<DrawApiController> _log;

    public DrawApiController(DrawService draw, IStringLocalizer<SharedResource> localizer, ILogger<DrawApiController> log)
    {
        _draw = draw;
        L = localizer;
        _log = log;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Draw([FromBody] DrawRequest req)
    {
        DrawOutcome r;
        try
        {
            r = await _draw.DrawAsync(req.PrizeId, req.RequestId);
        }
        catch (Exception ex) when (DbErrors.IsConnectionError(ex))
        {
            // Màn chiếu sẽ tự thử lại với cùng RequestId → không bị quay trùng nếu lượt này thực ra đã ghi xong
            _log.LogWarning(ex, "Draw on prize {PrizeId} failed: database unreachable", req.PrizeId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ApiError(L["Mất kết nối database, đang thử lại…"]));
        }
        return r.Status switch
        {
            DrawStatus.Ok => Ok(new DrawResponse(r.BatchId, r.Winners.Select(ResultViewDto.From).ToList(), r.WonAfter, r.Quantity)),
            DrawStatus.PrizeFull => Conflict(new ApiError(L["Giải này đã đủ người."])),
            DrawStatus.NotEnoughPeople => Conflict(new ApiError(L["Không đủ người hợp lệ: cần {0}, chỉ còn {1}.", r.Take, r.PoolSize])),
            _ => NotFound(new ApiError(L["Giải không tồn tại, đã tắt hoặc sự kiện không còn được chiếu."]))
        };
    }
}
