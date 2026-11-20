using LuckyDraw.Web.Data.Repositories;
using LuckyDraw.Web.Models.Entities;
using Microsoft.AspNetCore.Mvc;

namespace LuckyDraw.Web.Areas.Admin.Controllers;

public class DashboardViewModel
{
    public Event? Event { get; init; }
    public ParticipantStats Stats { get; init; } = new();
    public IReadOnlyList<Prize> Prizes { get; init; } = Array.Empty<Prize>();
    public IReadOnlyList<DrawResultRow> RecentResults { get; init; } = Array.Empty<DrawResultRow>();
    public IReadOnlyList<AuditLog> RecentLogs { get; init; } = Array.Empty<AuditLog>();
    public int DepartmentCount { get; init; }
}

public class DashboardController : AdminControllerBase
{
    private readonly PrizeRepository _prizes;
    private readonly ParticipantRepository _participants;
    private readonly ResultRepository _results;
    private readonly AuditRepository _audit;
    private readonly DepartmentRepository _departments;

    public DashboardController(PrizeRepository prizes, ParticipantRepository participants, ResultRepository results,
        AuditRepository audit, DepartmentRepository departments)
    {
        _prizes = prizes;
        _participants = participants;
        _results = results;
        _audit = audit;
        _departments = departments;
    }

    public async Task<IActionResult> Index()
    {
        var ev = await WorkingEventAsync();
        var departments = await _departments.ListAsync();
        if (ev is null) return View(new DashboardViewModel { DepartmentCount = departments.Count });

        return View(new DashboardViewModel
        {
            Event = ev,
            Stats = await _participants.StatsAsync(ev.EventId),
            Prizes = await _prizes.ListByEventAsync(ev.EventId),
            RecentResults = await _results.RecentAsync(ev.EventId, 8),
            RecentLogs = (await _audit.PageAsync(ev.EventId, null, 1, 8)).Items,
            DepartmentCount = departments.Count
        });
    }
}
