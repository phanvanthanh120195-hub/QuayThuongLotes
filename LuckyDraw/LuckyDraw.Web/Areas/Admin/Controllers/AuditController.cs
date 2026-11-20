using LuckyDraw.Web.Data.Repositories;
using LuckyDraw.Web.Models.Entities;
using Microsoft.AspNetCore.Mvc;

namespace LuckyDraw.Web.Areas.Admin.Controllers;

public class AuditViewModel
{
    public PagedList<AuditLog> Page { get; init; } = new();
    public IReadOnlyList<string> Actions { get; init; } = Array.Empty<string>();
    public string? Action { get; init; }
    public bool AllEvents { get; init; }
}

public class AuditController : AdminControllerBase
{
    private readonly AuditRepository _audit;
    public AuditController(AuditRepository audit) => _audit = audit;

    // Tham số tên "act" (không dùng "action" vì trùng route value của MVC)
    public async Task<IActionResult> Index(string? act, bool all = false, int page = 1)
    {
        var ev = await WorkingEventAsync();
        int? eventId = all ? null : ev?.EventId;
        return View(new AuditViewModel
        {
            Page = await _audit.PageAsync(eventId, act, page),
            Actions = await _audit.ActionsAsync(),
            Action = act,
            AllEvents = all || ev is null
        });
    }
}
