using LuckyDraw.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace LuckyDraw.Web.Services;

public class RealtimeNotifier
{
    private readonly IHubContext<DrawHub> _hub;
    private readonly ILogger<RealtimeNotifier> _log;

    public RealtimeNotifier(IHubContext<DrawHub> hub, ILogger<RealtimeNotifier> log)
    {
        _hub = hub;
        _log = log;
    }

    public Task ConfigChanged(int eventId) => Send("ConfigChanged", eventId);
    public Task ResultChanged(int eventId, int? prizeId) => Send("ResultChanged", eventId, prizeId);
    public Task ActiveEventChanged() => Send("ActiveEventChanged");

    private async Task Send(string method, params object?[] args)
    {
        try
        {
            await _hub.Clients.All.SendCoreAsync(method, args);
        }
        catch (Exception ex)
        {
            // Không để lỗi đẩy realtime làm hỏng thao tác đã lưu xong
            _log.LogWarning(ex, "SignalR {Method} failed", method);
        }
    }
}
