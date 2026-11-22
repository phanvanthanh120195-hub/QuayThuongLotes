using Dapper;
using LuckyDraw.Web.Models.Entities;
using LuckyDraw.Web.Services;

namespace LuckyDraw.Web.Data.Repositories;

public enum RestoreOutcome
{
    Restored,
    NotFound,
    NotRemoved,
    PrizeFull,
    AlreadyWon
}

public class ResultRepository
{
    private readonly DbConnectionFactory _db;
    private readonly AuditRepository _audit;

    public ResultRepository(DbConnectionFactory db, AuditRepository audit)
    {
        _db = db;
        _audit = audit;
    }

    private const string Select = @"
        SELECT r.result_id, r.event_id, r.prize_id, r.participant_id, r.batch_id, r.drawn_at, r.drawn_by,
               r.is_removed, r.removed_reason, r.removed_at, r.removed_by,
               pa.employee_code, pa.full_name, pa.full_name_zh, pa.position, pa.photo_path,
               COALESCE(pa.department_vi, d.name_vi) AS department_name_vi,
                   COALESCE(pa.department_zh, d.name_zh_tw) AS department_name_zh_tw,
                   COALESCE(pa.department_zh, d.name_zh_cn) AS department_name_zh_cn,
                   CASE WHEN pa.department_vi IS NULL AND pa.department_zh IS NULL THEN d.name_en END AS department_name_en,
               z.name_vi AS prize_name_vi, z.name_zh_tw AS prize_name_zh_tw, z.name_zh_cn AS prize_name_zh_cn,
               z.name_en AS prize_name_en, z.sort_order AS prize_sort_order
        FROM luckydraw.draw_result r
        JOIN luckydraw.participant pa ON pa.participant_id = r.participant_id
        JOIN luckydraw.prize z ON z.prize_id = r.prize_id
        LEFT JOIN luckydraw.department d ON d.department_id = pa.department_id";

    public async Task<PagedList<DrawResultRow>> ListAsync(int eventId, bool removed, int? prizeId, string? q, int page, int pageSize = 50)
    {
        page = Math.Max(1, page);
        var sql = Select.Replace("SELECT r.result_id", "SELECT count(*) OVER () AS total_count, r.result_id") + @"
            WHERE r.event_id = @eventId
              AND r.is_removed = @removed
              AND (@prizeId::int IS NULL OR r.prize_id = @prizeId)
              AND (@q = '' OR pa.employee_code ILIKE @q OR pa.full_name_zh ILIKE @q OR " + VnText.Sql("pa.full_name") + @" LIKE @qf)   -- tên: tìm không dấu
            ORDER BY " + (removed ? "r.removed_at DESC" : "z.sort_order, r.drawn_at, r.result_id") + @"
            LIMIT @pageSize OFFSET @offset";
        await using var conn = await _db.OpenAsync();
        var rows = (await conn.QueryAsync<DrawResultRow>(sql,
            new { eventId, removed, prizeId, q = ParticipantRepository.LikePattern(q), qf = VnText.FoldPattern(q), pageSize, offset = (page - 1) * pageSize })).AsList();
        return new PagedList<DrawResultRow>
        {
            Items = rows,
            Page = page,
            PageSize = pageSize,
            Total = rows.Count > 0 ? (int)rows[0].TotalCount : 0
        };
    }

    public async Task<IReadOnlyList<DrawResultRow>> ListAllActiveAsync(int eventId)
    {
        await using var conn = await _db.OpenAsync();
        return (await conn.QueryAsync<DrawResultRow>(Select + @"
            WHERE r.event_id = @eventId AND NOT r.is_removed
            ORDER BY z.sort_order, r.drawn_at, r.result_id", new { eventId })).AsList();
    }

    public async Task<IReadOnlyList<DrawResultRow>> RecentAsync(int eventId, int take)
    {
        await using var conn = await _db.OpenAsync();
        return (await conn.QueryAsync<DrawResultRow>(Select + @"
            WHERE r.event_id = @eventId AND NOT r.is_removed
            ORDER BY r.drawn_at DESC, r.result_id DESC
            LIMIT @take", new { eventId, take })).AsList();
    }

    public async Task<IReadOnlyList<DrawResultRow>> ListBatchAsync(Guid batchId)
    {
        await using var conn = await _db.OpenAsync();
        return (await conn.QueryAsync<DrawResultRow>(Select + @"
            WHERE r.batch_id = @batchId
            ORDER BY r.result_id", new { batchId })).AsList();
    }

    /// <summary>
    /// Vắng mặt – quay lại (mục 6 thiết kế): xoá mềm kết quả (lý do = vắng mặt) và đánh dấu người đó vắng mặt.
    /// Trả về prize_id của kết quả, null nếu kết quả không tồn tại hoặc đã xử lý rồi.
    /// </summary>
    public async Task<int?> MarkAbsentAsync(int eventId, long resultId, string user, AuditLog audit)
    {
        await using var conn = await _db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        // RETURNING: không có dòng nào nghĩa là kết quả không tồn tại hoặc đã được xử lý rồi
        var row = await conn.QuerySingleOrDefaultAsync<AbsentRow>(@"
            UPDATE luckydraw.draw_result
               SET is_removed = TRUE, removed_reason = @reason, removed_at = now(), removed_by = @user
             WHERE result_id = @resultId AND event_id = @eventId AND NOT is_removed
            RETURNING participant_id, prize_id",
            new { eventId, resultId, user, reason = RemovedReasons.Absent }, tx);
        if (row is null) { await tx.RollbackAsync(); return null; }

        await conn.ExecuteAsync(
            "UPDATE luckydraw.participant SET is_absent = TRUE, updated_at = now() WHERE participant_id = @id AND event_id = @eventId",
            new { id = row.ParticipantId, eventId }, tx);
        await _audit.InsertAsync(audit, conn, tx);
        await tx.CommitAsync();
        return row.PrizeId;
    }

    private sealed class AbsentRow
    {
        public int ParticipantId { get; set; }
        public int PrizeId { get; set; }
    }

    /// <summary>Admin xoá 1 kết quả (xoá mềm, lý do = Admin xoá). Người đó có thể được quay lại.</summary>
    public async Task<bool> SoftDeleteAsync(int eventId, long resultId, string user, AuditLog audit)
    {
        await using var conn = await _db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        var n = await conn.ExecuteAsync(@"
            UPDATE luckydraw.draw_result
               SET is_removed = TRUE, removed_reason = @reason, removed_at = now(), removed_by = @user
             WHERE result_id = @resultId AND event_id = @eventId AND NOT is_removed",
            new { eventId, resultId, user, reason = RemovedReasons.AdminDeleted }, tx);
        if (n != 1) { await tx.RollbackAsync(); return false; }
        await _audit.InsertAsync(audit, conn, tx);
        await tx.CommitAsync();
        return true;
    }

    /// <summary>
    /// Khôi phục kết quả đã xoá mềm. Khoá dòng giải (FOR UPDATE) giống lúc quay để không vượt số lượng
    /// khi MC đang quay cùng lúc. Nếu lý do là vắng mặt thì bỏ cờ vắng mặt của người đó.
    /// </summary>
    public async Task<RestoreOutcome> RestoreAsync(int eventId, long resultId, AuditLog audit)
    {
        await using var conn = await _db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var row = await conn.QuerySingleOrDefaultAsync<(int PrizeId, int ParticipantId, bool IsRemoved, short? RemovedReason)>(@"
            SELECT prize_id, participant_id, is_removed, removed_reason
            FROM luckydraw.draw_result WHERE result_id = @resultId AND event_id = @eventId",
            new { eventId, resultId }, tx);
        if (row == default) { await tx.RollbackAsync(); return RestoreOutcome.NotFound; }
        if (!row.IsRemoved) { await tx.RollbackAsync(); return RestoreOutcome.NotRemoved; }

        var quantity = await conn.ExecuteScalarAsync<int>(
            "SELECT quantity FROM luckydraw.prize WHERE prize_id = @PrizeId FOR UPDATE", new { row.PrizeId }, tx);
        var active = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM luckydraw.draw_result WHERE prize_id = @PrizeId AND NOT is_removed", new { row.PrizeId }, tx);
        if (active >= quantity) { await tx.RollbackAsync(); return RestoreOutcome.PrizeFull; }

        var alreadyWon = await conn.ExecuteScalarAsync<bool>(@"
            SELECT EXISTS (SELECT 1 FROM luckydraw.draw_result
                            WHERE event_id = @eventId AND participant_id = @ParticipantId AND NOT is_removed)",
            new { eventId, row.ParticipantId }, tx);
        if (alreadyWon) { await tx.RollbackAsync(); return RestoreOutcome.AlreadyWon; }

        await conn.ExecuteAsync(@"
            UPDATE luckydraw.draw_result
               SET is_removed = FALSE, removed_reason = NULL, removed_at = NULL, removed_by = NULL
             WHERE result_id = @resultId AND is_removed", new { resultId }, tx);
        if (row.RemovedReason == RemovedReasons.Absent)
        {
            await conn.ExecuteAsync(
                "UPDATE luckydraw.participant SET is_absent = FALSE, updated_at = now() WHERE participant_id = @ParticipantId",
                new { row.ParticipantId }, tx);
        }
        await _audit.InsertAsync(audit, conn, tx);
        await tx.CommitAsync();
        return RestoreOutcome.Restored;
    }

    /// <summary>Reset toàn bộ: xoá mềm mọi kết quả còn hiệu lực và bỏ cờ vắng mặt. Trả về số kết quả bị xoá.</summary>
    public async Task<int> ResetAllAsync(int eventId, string user, AuditLog audit)
    {
        await using var conn = await _db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        var n = await conn.ExecuteAsync(@"
            UPDATE luckydraw.draw_result
               SET is_removed = TRUE, removed_reason = @reason, removed_at = now(), removed_by = @user
             WHERE event_id = @eventId AND NOT is_removed",
            new { eventId, user, reason = RemovedReasons.Reset }, tx);
        await conn.ExecuteAsync(
            "UPDATE luckydraw.participant SET is_absent = FALSE, updated_at = now() WHERE event_id = @eventId AND is_absent",
            new { eventId }, tx);
        audit.Detail = System.Text.Json.JsonSerializer.Serialize(new { removed = n });
        await _audit.InsertAsync(audit, conn, tx);
        await tx.CommitAsync();
        return n;
    }
}
