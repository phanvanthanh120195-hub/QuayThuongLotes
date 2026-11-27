using System.Collections.Concurrent;

namespace LuckyDraw.Web.Services;

/// <summary>
/// Chống dò mật khẩu: đếm số lần đăng nhập sai và khoá tạm.
///   - Theo TÊN ĐĂNG NHẬP: sai 5 lần trong 5 phút → khoá tên đó 5 phút.
///   - Theo MÁY (IP): sai 20 lần trong 10 phút → khoá máy đó 10 phút (chặn dò lần lượt nhiều tên).
/// Áp dụng cả với tên không tồn tại để không lộ tên nào có thật. Đăng nhập đúng thì xoá bộ đếm của tên đó.
/// Lưu trong bộ nhớ (app chạy 1 máy chủ): khởi động lại app thì bộ đếm về 0.
/// </summary>
public sealed class LoginThrottle
{
    public const int UserMaxFailures = 5;
    public static readonly TimeSpan UserWindow = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan UserLockout = TimeSpan.FromMinutes(5);

    public const int IpMaxFailures = 20;
    public static readonly TimeSpan IpWindow = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan IpLockout = TimeSpan.FromMinutes(10);

    private sealed class Entry
    {
        public readonly Queue<DateTime> Failures = new();
        public DateTime LockedUntil;
    }

    private readonly ConcurrentDictionary<string, Entry> _entries = new();
    private readonly TimeProvider _clock;

    public LoginThrottle(TimeProvider? clock = null) => _clock = clock ?? TimeProvider.System;

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;
    private static string UserKey(string userName) => "u:" + userName.Trim().ToLowerInvariant();
    private static string IpKey(string? ip) => "ip:" + (string.IsNullOrEmpty(ip) ? "?" : ip);

    /// <summary>Còn bị khoá bao lâu (null = được phép thử). Lấy thời gian khoá dài hơn giữa tên và IP.</summary>
    public TimeSpan? LockedFor(string userName, string? ip)
    {
        var now = Now;
        TimeSpan? Remaining(string key) =>
            _entries.TryGetValue(key, out var e) && e.LockedUntil > now ? e.LockedUntil - now : null;
        var a = Remaining(UserKey(userName));
        var b = Remaining(IpKey(ip));
        if (a is null) return b;
        if (b is null) return a;
        return a > b ? a : b;
    }

    /// <summary>Ghi nhận 1 lần sai. Trả về true nếu lần sai này làm tên hoặc IP bị khoá.</summary>
    public bool RecordFailure(string userName, string? ip)
    {
        var lockedUser = Hit(UserKey(userName), UserMaxFailures, UserWindow, UserLockout);
        var lockedIp = Hit(IpKey(ip), IpMaxFailures, IpWindow, IpLockout);
        Cleanup();
        return lockedUser || lockedIp;
    }

    /// <summary>Đăng nhập đúng: xoá bộ đếm của tên đăng nhập (bộ đếm theo IP giữ nguyên).</summary>
    public void RecordSuccess(string userName) => _entries.TryRemove(UserKey(userName), out _);

    private bool Hit(string key, int max, TimeSpan window, TimeSpan lockout)
    {
        var now = Now;
        var e = _entries.GetOrAdd(key, _ => new Entry());
        lock (e)
        {
            while (e.Failures.Count > 0 && now - e.Failures.Peek() > window) e.Failures.Dequeue();
            e.Failures.Enqueue(now);
            if (e.Failures.Count < max) return false;
            e.LockedUntil = now + lockout;
            e.Failures.Clear();
            return true;
        }
    }

    /// <summary>Dọn các mục đã hết hạn để bộ nhớ không phình khi bị dò nhiều tên.</summary>
    private void Cleanup()
    {
        if (_entries.Count < 500) return;
        var now = Now;
        foreach (var (key, e) in _entries)
        {
            lock (e)
            {
                var stale = e.LockedUntil <= now && (e.Failures.Count == 0 || now - e.Failures.Peek() > IpWindow);
                if (stale) _entries.TryRemove(key, out _);
            }
        }
    }
}
