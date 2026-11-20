using LuckyDraw.Web.Data.Repositories;
using LuckyDraw.Web.Models.Entities;

namespace LuckyDraw.Web.Services;

/// <summary>
/// "Sự kiện đang làm việc" của Admin (lưu cookie, độc lập với sự kiện đang chiếu).
/// Mặc định: sự kiện đang chiếu, không có thì sự kiện mới nhất.
/// </summary>
public class AdminEventContext
{
    public const string CookieName = "ld_admin_event";

    private readonly EventRepository _events;
    private readonly IHttpContextAccessor _http;
    private Event? _current;
    private bool _loaded;
    private int? _pendingId;
    private IReadOnlyList<Event>? _all;

    public AdminEventContext(EventRepository events, IHttpContextAccessor http)
    {
        _events = events;
        _http = http;
    }

    public async Task<Event?> GetAsync()
    {
        if (_loaded) return _current;
        _loaded = true;
        var ctx = _http.HttpContext;
        if (_pendingId is int pending)
        {
            _current = await _events.GetAsync(pending);
        }
        else if (ctx != null && int.TryParse(ctx.Request.Cookies[CookieName], out var id))
        {
            _current = await _events.GetAsync(id);
        }
        _current ??= await _events.GetDefaultAsync();
        return _current;
    }

    public async Task<IReadOnlyList<Event>> ListAsync() => _all ??= await _events.ListBasicAsync();

    public void Set(int eventId)
    {
        _http.HttpContext?.Response.Cookies.Append(CookieName, eventId.ToString(), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.AddDays(60)
        });
        _pendingId = eventId;
        _loaded = false;
        _current = null;
        _all = null;
    }
}
