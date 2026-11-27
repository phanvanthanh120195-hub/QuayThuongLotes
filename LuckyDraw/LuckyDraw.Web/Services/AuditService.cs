using System.Text.Json;
using LuckyDraw.Web.Data.Repositories;
using LuckyDraw.Web.Models.Entities;

namespace LuckyDraw.Web.Services;

/// <summary>Ghi nhật ký. Người thao tác lấy từ User.Identity.Name phía server, không nhận từ form.</summary>
public class AuditService
{
    private readonly AuditRepository _repo;
    private readonly IHttpContextAccessor _http;

    public AuditService(AuditRepository repo, IHttpContextAccessor http)
    {
        _repo = repo;
        _http = http;
    }

    public string CurrentUser => _http.HttpContext?.User.Identity?.Name ?? "system";

    public AuditLog Build(string action, int? eventId, string? entity = null, object? entityId = null, object? detail = null)
        => new()
        {
            Action = action,
            EventId = eventId,
            Entity = entity,
            EntityId = entityId?.ToString(),
            Detail = detail is null ? null : JsonSerializer.Serialize(detail),
            UserName = CurrentUser,
            IpAddress = _http.HttpContext?.Connection.RemoteIpAddress?.ToString()
        };

    public Task LogAsync(string action, int? eventId, string? entity = null, object? entityId = null, object? detail = null)
        => _repo.InsertAsync(Build(action, eventId, entity, entityId, detail));
}

public static class AuditActions
{
    public const string Login = "LOGIN";
    public const string LoginFailed = "LOGIN_FAILED";
    /// <summary>Bị khoá tạm vì đăng nhập sai quá nhiều lần (LoginThrottle).</summary>
    public const string LoginLocked = "LOGIN_LOCKED";
    public const string EventCreate = "EVENT_CREATE";
    public const string EventUpdate = "EVENT_UPDATE";
    public const string EventDelete = "EVENT_DELETE";
    public const string EventActivate = "EVENT_ACTIVATE";
    public const string EventDeactivate = "EVENT_DEACTIVATE";
    public const string DepartmentCreate = "DEPARTMENT_CREATE";
    public const string DepartmentUpdate = "DEPARTMENT_UPDATE";
    public const string DepartmentDelete = "DEPARTMENT_DELETE";
    public const string PrizeCreate = "PRIZE_CREATE";
    public const string PrizeUpdate = "PRIZE_UPDATE";
    public const string PrizeDelete = "PRIZE_DELETE";
    public const string PrizeReorder = "PRIZE_REORDER";
    public const string ParticipantCreate = "PARTICIPANT_CREATE";
    public const string ParticipantUpdate = "PARTICIPANT_UPDATE";
    public const string ParticipantDelete = "PARTICIPANT_DELETE";
    public const string ParticipantDeleteAll = "PARTICIPANT_DELETE_ALL";
    public const string ParticipantAbsent = "PARTICIPANT_ABSENT";
    public const string ParticipantPhotos = "PARTICIPANT_PHOTOS";
    public const string Import = "IMPORT";
    public const string ThemeUpdate = "THEME_UPDATE";
    public const string SoundUpdate = "SOUND_UPDATE";
    public const string Draw = "DRAW";
    public const string MarkAbsent = "MARK_ABSENT";
    public const string ResultDelete = "RESULT_DELETE";
    public const string ResultRestore = "RESULT_RESTORE";
    public const string ResultReset = "RESULT_RESET";
    public const string UserCreate = "USER_CREATE";
    public const string UserUpdate = "USER_UPDATE";
    public const string UserPassword = "USER_PASSWORD";
    public const string UserDelete = "USER_DELETE";
}
