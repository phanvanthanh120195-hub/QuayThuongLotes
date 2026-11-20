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

    public DrawApiController(DrawService draw, IStringLocalizer<SharedResource> localizer)
    {
        _draw = draw;
        L = localizer;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Draw([FromBody] DrawRequest req)
    {
        var r = await _draw.DrawAsync(req.PrizeId);
        return r.Status switch
        {
            DrawStatus.Ok => Ok(new DrawResponse(r.BatchId, r.Winners.Select(ResultViewDto.From).ToList(), r.WonAfter, r.Quantity)),
            DrawStatus.PrizeFull => Conflict(new ApiError(L["Giải này đã đủ người."])),
            DrawStatus.NotEnoughPeople => Conflict(new ApiError(L["Không đủ người hợp lệ: cần {0}, chỉ còn {1}.", r.Take, r.PoolSize])),
            _ => NotFound(new ApiError(L["Giải không tồn tại, đã tắt hoặc sự kiện không còn được chiếu."]))
        };
    }
}
