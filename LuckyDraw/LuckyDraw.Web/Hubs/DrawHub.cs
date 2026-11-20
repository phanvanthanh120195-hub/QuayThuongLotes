using Microsoft.AspNetCore.SignalR;

namespace LuckyDraw.Web.Hubs;

/// <summary>
/// Màn chiếu chỉ lắng nghe; mọi thay đổi đều đi qua C# rồi được đẩy xuống bằng RealtimeNotifier.
///   ConfigChanged(eventId)            – Admin lưu giải / giao diện / âm thanh / người tham dự
///   ResultChanged(eventId, prizeId)   – quay xong, vắng mặt, Admin khôi phục / reset (prizeId = null: nhiều giải)
///   ActiveEventChanged()              – Admin đổi sự kiện đang chiếu
/// </summary>
public class DrawHub : Hub
{
}
