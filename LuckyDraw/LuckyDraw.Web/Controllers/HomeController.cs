using System.Diagnostics;
using LuckyDraw.Web.Data.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace LuckyDraw.Web.Controllers;

/// <summary>Trang chính = màn chiếu quay số. Mặc định ai cũng xem được; đặt "LuckyDraw:PublicView": false để bắt đăng nhập.</summary>
public class HomeController : Controller
{
    private readonly EventRepository _events;
    private readonly IConfiguration _cfg;

    public HomeController(EventRepository events, IConfiguration cfg)
    {
        _events = events;
        _cfg = cfg;
    }

    public async Task<IActionResult> Index()
    {
        if (!_cfg.GetValue("LuckyDraw:PublicView", true) && User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }
        return View(await _events.GetActiveAsync());
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(model: Activity.Current?.Id ?? HttpContext.TraceIdentifier);
}
