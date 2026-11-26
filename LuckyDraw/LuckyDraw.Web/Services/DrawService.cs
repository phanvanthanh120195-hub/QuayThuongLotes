using System.Security.Cryptography;
using LuckyDraw.Web.Data;
using LuckyDraw.Web.Data.Repositories;
using LuckyDraw.Web.Models.Entities;

namespace LuckyDraw.Web.Services;

public enum DrawStatus
{
    Ok,
    PrizeUnavailable,
    PrizeFull,
    NotEnoughPeople
}

public record DrawOutcome(DrawStatus Status, Guid BatchId, IReadOnlyList<DrawResultRow> Winners,
    int Quantity, int WonAfter, int PoolSize, int Take);

/// <summary>
/// Logic quay (mục 5 thiết kế). Kết quả được quyết định ở server TRƯỚC, client chỉ diễn hiệu ứng.
///   BEGIN → khoá dòng giải (FOR UPDATE) → đếm còn lại → lấy pool → chọn ngẫu nhiên (RandomNumberGenerator
///   + Fisher–Yates) → INSERT kết quả + nhật ký → COMMIT → SignalR ResultChanged.
/// </summary>
public class DrawService
{
    private const int MaxAttempts = 3;

    private readonly DbConnectionFactory _db;
    private readonly DrawRepository _draws;
    private readonly ResultRepository _results;
    private readonly AuditRepository _auditRepo;
    private readonly AuditService _audit;
    private readonly RealtimeNotifier _realtime;
    private readonly ILogger<DrawService> _log;

    public DrawService(DbConnectionFactory db, DrawRepository draws, ResultRepository results, AuditRepository auditRepo,
        AuditService audit, RealtimeNotifier realtime, ILogger<DrawService> log)
    {
        _db = db;
        _draws = draws;
        _results = results;
        _auditRepo = auditRepo;
        _audit = audit;
        _realtime = realtime;
        _log = log;
    }

    /// <param name="requestId">
    /// Mã lượt quay từ màn chiếu (dùng làm batch_id). Thử lại cùng mã sau lỗi mạng thì trả lại kết quả đã ghi,
    /// không quay thêm – tránh trường hợp DB đã COMMIT nhưng phản hồi bị mất.
    /// </param>
    public async Task<DrawOutcome> DrawAsync(int prizeId, Guid? requestId = null)
    {
        var batchId = requestId is { } id && id != Guid.Empty ? id : Guid.NewGuid();
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await DrawOnceAsync(prizeId, batchId);
            }
            catch (Exception ex) when (attempt < MaxAttempts
                                       && PgErrors.Is(ex, PgErrors.UniqueViolation, "ux_draw_result_one_win"))
            {
                // Hai GIẢI KHÁC NHAU quay cùng lúc chọn trúng cùng 1 người (khoá dòng chỉ theo giải).
                // DB chặn bằng unique index → quay lại từ đầu với pool mới.
                _log.LogWarning("Draw collision on prize {PrizeId}, retry {Attempt}", prizeId, attempt);
            }
        }
    }

    private async Task<DrawOutcome> DrawOnceAsync(int prizeId, Guid batchId)
    {
        LockedPrize prize;
        int take, poolSize, wonAfter;

        await using (var conn = await _db.OpenAsync())
        await using (var tx = await conn.BeginTransactionAsync())
        {
            var locked = await _draws.LockPrizeAsync(conn, tx, prizeId);
            if (locked is null)
            {
                await tx.RollbackAsync();
                return new(DrawStatus.PrizeUnavailable, Guid.Empty, Array.Empty<DrawResultRow>(), 0, 0, 0, 0);
            }
            prize = locked;

            var active = await _draws.CountActiveAsync(conn, tx, prizeId);

            // Thử lại một lượt đã ghi xong (đang khoá dòng giải nên không chạy song song được với chính nó)
            if (await _draws.BatchExistsAsync(conn, tx, prizeId, batchId))
            {
                await tx.RollbackAsync();
                _log.LogInformation("Draw replay on prize {PrizeId}, batch {BatchId}", prizeId, batchId);
                var done = await _results.ListBatchAsync(batchId);
                return new(DrawStatus.Ok, batchId, done, prize.Quantity, active, 0, done.Count);
            }
            var remaining = prize.Quantity - active;
            if (remaining <= 0)
            {
                await tx.RollbackAsync();
                return new(DrawStatus.PrizeFull, Guid.Empty, Array.Empty<DrawResultRow>(), prize.Quantity, active, 0, 0);
            }

            // Quay lại người vắng mặt thì chỉ còn 1 suất → chỉ quay 1 người
            take = Math.Min(prize.DrawPerTurn, remaining);
            var pool = await _draws.GetPoolAsync(conn, tx, prize);
            poolSize = pool.Count;
            if (pool.Count < take)
            {
                await tx.RollbackAsync();
                return new(DrawStatus.NotEnoughPeople, Guid.Empty, Array.Empty<DrawResultRow>(), prize.Quantity, active, poolSize, take);
            }

            var chosen = PickRandom(pool, take);
            await _draws.InsertResultsAsync(conn, tx, prize, chosen.Select(c => c.ParticipantId).ToList(), batchId, _audit.CurrentUser);
            await _auditRepo.InsertAsync(_audit.Build(AuditActions.Draw, prize.EventId, "prize", prizeId, new
            {
                batch = batchId,
                codes = chosen.Select(c => c.EmployeeCode),
                pool = poolSize
            }), conn, tx);
            await tx.CommitAsync();
            wonAfter = active + take;
        }

        await _realtime.ResultChanged(prize.EventId, prizeId);
        var winners = await _results.ListBatchAsync(batchId);
        return new(DrawStatus.Ok, batchId, winners, prize.Quantity, wonAfter, poolSize, take);
    }

    /// <summary>Chọn k người không trùng: Fisher–Yates dừng sau k bước, số ngẫu nhiên mật mã.</summary>
    public static List<T> PickRandom<T>(IReadOnlyList<T> pool, int k)
    {
        if (k < 0 || k > pool.Count) throw new ArgumentOutOfRangeException(nameof(k));
        var arr = pool.ToArray();
        for (var i = 0; i < k; i++)
        {
            var j = i + RandomNumberGenerator.GetInt32(arr.Length - i);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
        return arr.Take(k).ToList();
    }
}
