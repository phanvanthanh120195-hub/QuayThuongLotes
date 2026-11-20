using LuckyDraw.Web.Models.Entities;
using LuckyDraw.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace LuckyDraw.Web.Areas.Admin.Controllers;

/// <summary>Toàn bộ /admin chỉ dành cho role Admin (đặt ở mức Area).</summary>
[Area("Admin")]
[Authorize(Roles = Roles.Admin)]
public abstract class AdminControllerBase : Controller
{
    private IStringLocalizer<SharedResource>? _l;

    protected IStringLocalizer<SharedResource> L =>
        _l ??= HttpContext.RequestServices.GetRequiredService<IStringLocalizer<SharedResource>>();

    protected AdminEventContext EventContext => HttpContext.RequestServices.GetRequiredService<AdminEventContext>();
    protected AuditService Audit => HttpContext.RequestServices.GetRequiredService<AuditService>();
    protected RealtimeNotifier Realtime => HttpContext.RequestServices.GetRequiredService<RealtimeNotifier>();

    protected void Success(string message) => TempData["flash.success"] = message;
    protected void Error(string message) => TempData["flash.error"] = message;

    protected Task<Event?> WorkingEventAsync() => EventContext.GetAsync();

    protected IActionResult NoEvent()
    {
        Error(L["Chưa có sự kiện nào. Hãy tạo sự kiện trước."]);
        return RedirectToAction("Create", "Events");
    }

    protected string UploadError(UploadResult r)
        => r.ErrorArg is null ? L[r.Error ?? ""] : L[r.Error ?? "", r.ErrorArg];
}
