using Dapper;
using LuckyDraw.Web.Models.Entities;
using Npgsql;

namespace LuckyDraw.Web.Data.Repositories;

/// <summary>Cấu hình giải đã được khoá dòng (FOR UPDATE) trong transaction quay.</summary>
public class LockedPrize
{
    public int PrizeId { get; set; }
    public int EventId { get; set; }
    public int Quantity { get; set; }
    public int DrawPerTurn { get; set; }
    public int? MinSeniorityMonths { get; set; }
    public int? MaxSeniorityMonths { get; set; }
    public DateTime EventDate { get; set; }
}

public class PoolMember
{
    public int ParticipantId { get; set; }
    public string EmployeeCode { get; set; } = "";
}

/// <summary>SQL của logic quay (mục 5.2 thiết kế). Mọi hàm chạy trong transaction do DrawService mở.</summary>
public class DrawRepository
{
    /// <summary>Bước 2: khoá dòng giải. Máy thứ 2 bấm cùng lúc phải CHỜ tới khi máy 1 commit.</summary>
    public Task<LockedPrize?> LockPrizeAsync(NpgsqlConnection conn, NpgsqlTransaction tx, int prizeId)
        => conn.QuerySingleOrDefaultAsync<LockedPrize>(@"
            SELECT p.prize_id, p.event_id, p.quantity, p.draw_per_turn,
                   p.min_seniority_months, p.max_seniority_months, e.event_date
            FROM luckydraw.prize p
            JOIN luckydraw.event e ON e.event_id = p.event_id
            WHERE p.prize_id = @prizeId AND p.is_active AND e.is_active
            FOR UPDATE OF p", new { prizeId }, tx);

    /// <summary>Bước 3: số kết quả còn hiệu lực của giải.</summary>
    public Task<int> CountActiveAsync(NpgsqlConnection conn, NpgsqlTransaction tx, int prizeId)
        => conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM luckydraw.draw_result WHERE prize_id = @prizeId AND NOT is_removed", new { prizeId }, tx);

    /// <summary>Bước 5: pool người hợp lệ (có mặt, đúng thâm niên, chưa trúng giải nào của sự kiện).</summary>
    public async Task<List<PoolMember>> GetPoolAsync(NpgsqlConnection conn, NpgsqlTransaction tx, LockedPrize p)
        => (await conn.QueryAsync<PoolMember>(@"
            SELECT pa.participant_id, pa.employee_code
            FROM luckydraw.participant pa
            WHERE pa.event_id = @EventId
              AND NOT pa.is_absent
              AND (@MinSeniorityMonths::int IS NULL
                   OR pa.join_date <= (@EventDate::date - make_interval(months => @MinSeniorityMonths::int))::date)
              AND (@MaxSeniorityMonths::int IS NULL
                   OR pa.join_date >  (@EventDate::date - make_interval(months => @MaxSeniorityMonths::int))::date)
              AND NOT EXISTS (SELECT 1 FROM luckydraw.draw_result r
                               WHERE r.event_id = pa.event_id
                                 AND r.participant_id = pa.participant_id
                                 AND NOT r.is_removed)", p, tx)).AsList();

    /// <summary>Bước 7: ghi kết quả (cùng batch_id) trong 1 lệnh.</summary>
    /// <summary>Lượt quay (batch) này đã được ghi chưa – dùng khi màn chiếu thử lại sau lỗi mạng.</summary>
    public Task<bool> BatchExistsAsync(NpgsqlConnection conn, NpgsqlTransaction tx, int prizeId, Guid batchId)
        => conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM luckydraw.draw_result WHERE prize_id = @prizeId AND batch_id = @batchId)",
            new { prizeId, batchId }, tx);

    public Task<int> InsertResultsAsync(NpgsqlConnection conn, NpgsqlTransaction tx, LockedPrize p,
        IReadOnlyList<int> participantIds, Guid batchId, string user)
        => conn.ExecuteAsync(@"
            INSERT INTO luckydraw.draw_result (event_id, prize_id, participant_id, batch_id, drawn_by)
            SELECT @eventId, @prizeId, x.id, @batchId, @user
            FROM unnest(@ids::int[]) WITH ORDINALITY AS x(id, ord)
            ORDER BY x.ord",
            new { eventId = p.EventId, prizeId = p.PrizeId, ids = participantIds.ToArray(), batchId, user }, tx);
}
