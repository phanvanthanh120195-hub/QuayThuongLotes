using Dapper;
using LuckyDraw.Web.Models.Entities;
using Npgsql;

namespace LuckyDraw.Web.Data.Repositories;

public class PrizeRepository
{
    private readonly DbConnectionFactory _db;
    public PrizeRepository(DbConnectionFactory db) => _db = db;

    private const string Columns = @"p.prize_id, p.event_id, p.sort_order, p.name_vi, p.name_zh_tw, p.name_zh_cn, p.name_en,
        p.quantity, p.draw_per_turn, p.draw_type, p.spin_seconds, p.stop_seconds,
        p.min_seniority_months, p.max_seniority_months, p.image_path, p.is_active";

    /// <summary>Danh sách giải của sự kiện + đã trúng bao nhiêu + còn bao nhiêu người đủ điều kiện.</summary>
    public async Task<IReadOnlyList<Prize>> ListByEventAsync(int eventId)
    {
        const string sql = $@"
            SELECT {Columns},
                   (SELECT count(*) FROM luckydraw.draw_result r
                     WHERE r.prize_id = p.prize_id AND NOT r.is_removed)::int AS won_count,
                   (SELECT count(*) FROM luckydraw.participant pa
                     WHERE pa.event_id = p.event_id
                       AND NOT pa.is_absent
                       AND (p.min_seniority_months IS NULL
                            OR pa.join_date <= (e.event_date - make_interval(months => p.min_seniority_months))::date)
                       AND (p.max_seniority_months IS NULL
                            OR pa.join_date >  (e.event_date - make_interval(months => p.max_seniority_months))::date)
                       AND NOT EXISTS (SELECT 1 FROM luckydraw.draw_result r
                                        WHERE r.event_id = pa.event_id
                                          AND r.participant_id = pa.participant_id
                                          AND NOT r.is_removed))::int AS eligible_count
            FROM luckydraw.prize p
            JOIN luckydraw.event e ON e.event_id = p.event_id
            WHERE p.event_id = @eventId
            ORDER BY p.sort_order, p.prize_id";
        await using var conn = await _db.OpenAsync();
        return (await conn.QueryAsync<Prize>(sql, new { eventId })).AsList();
    }

    public async Task<Prize?> GetAsync(int eventId, int prizeId)
    {
        const string sql = $@"
            SELECT {Columns},
                   (SELECT count(*) FROM luckydraw.draw_result r WHERE r.prize_id = p.prize_id AND NOT r.is_removed)::int AS won_count
            FROM luckydraw.prize p
            WHERE p.prize_id = @prizeId AND p.event_id = @eventId";
        await using var conn = await _db.OpenAsync();
        return await conn.QuerySingleOrDefaultAsync<Prize>(sql, new { eventId, prizeId });
    }

    public async Task<int> InsertAsync(Prize p)
    {
        const string sql = @"
            INSERT INTO luckydraw.prize (event_id, sort_order, name_vi, name_zh_tw, name_zh_cn, name_en, quantity, draw_per_turn,
                                         draw_type, spin_seconds, stop_seconds, min_seniority_months, max_seniority_months,
                                         image_path, is_active)
            VALUES (@EventId,
                    CASE WHEN @SortOrder > 0 THEN @SortOrder
                         ELSE (SELECT coalesce(max(sort_order), 0) + 1 FROM luckydraw.prize WHERE event_id = @EventId) END,
                    @NameVi, @NameZhTw, @NameZhCn, @NameEn, @Quantity, @DrawPerTurn, @DrawType, @SpinSeconds, @StopSeconds,
                    @MinSeniorityMonths, @MaxSeniorityMonths, @ImagePath, @IsActive)
            RETURNING prize_id";
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteScalarAsync<int>(sql, p);
    }

    public async Task<bool> UpdateAsync(Prize p)
    {
        const string sql = @"
            UPDATE luckydraw.prize
               SET name_vi = @NameVi, name_zh_tw = @NameZhTw, name_zh_cn = @NameZhCn, name_en = @NameEn,
                   quantity = @Quantity, draw_per_turn = @DrawPerTurn, draw_type = @DrawType,
                   spin_seconds = @SpinSeconds, stop_seconds = @StopSeconds,
                   min_seniority_months = @MinSeniorityMonths, max_seniority_months = @MaxSeniorityMonths,
                   image_path = @ImagePath, is_active = @IsActive, updated_at = now()
             WHERE prize_id = @PrizeId AND event_id = @EventId";
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteAsync(sql, p) == 1;
    }

    public async Task<bool> SetActiveAsync(int eventId, int prizeId, bool active)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteAsync(
            "UPDATE luckydraw.prize SET is_active = @active, updated_at = now() WHERE prize_id = @prizeId AND event_id = @eventId",
            new { eventId, prizeId, active }) == 1;
    }

    /// <summary>Đánh lại thứ tự 1..n theo mảng id truyền vào.</summary>
    public async Task ReorderAsync(int eventId, IReadOnlyList<int> orderedIds)
    {
        const string sql = @"
            UPDATE luckydraw.prize p
               SET sort_order = x.ord, updated_at = now()
              FROM unnest(@ids::int[]) WITH ORDINALITY AS x(id, ord)
             WHERE p.prize_id = x.id AND p.event_id = @eventId";
        await using var conn = await _db.OpenAsync();
        await conn.ExecuteAsync(sql, new { eventId, ids = orderedIds.ToArray() });
    }

    /// <summary>Giải đã có kết quả (kể cả đã xoá mềm) thì DB chặn (FK) → PostgresException 23503.</summary>
    public async Task<bool> DeleteAsync(int eventId, int prizeId)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteAsync("DELETE FROM luckydraw.prize WHERE prize_id = @prizeId AND event_id = @eventId",
            new { eventId, prizeId }) == 1;
    }

    /// <summary>Import: thêm nhiều giải trong 1 lệnh. replaceAll = xoá hết giải cũ của sự kiện trước.</summary>
    public async Task<int> InsertManyAsync(int eventId, IReadOnlyList<Prize> rows, bool replaceAll, NpgsqlConnection conn, NpgsqlTransaction tx)
    {
        if (replaceAll)
        {
            await conn.ExecuteAsync("DELETE FROM luckydraw.prize WHERE event_id = @eventId", new { eventId }, tx);
        }
        const string sql = @"
            INSERT INTO luckydraw.prize (event_id, sort_order, name_vi, name_zh_tw, name_zh_cn, name_en, quantity, draw_per_turn,
                                         draw_type, spin_seconds, stop_seconds, min_seniority_months, max_seniority_months)
            SELECT @eventId, x.sort_order, x.name_vi, x.name_zh_tw, x.name_zh_cn, x.name_en, x.quantity, x.draw_per_turn,
                   x.draw_type, x.spin_seconds, x.stop_seconds, x.min_m, x.max_m
            FROM unnest(@Sort::int[], @NameVi::text[], @NameZhTw::text[], @NameZhCn::text[], @NameEn::text[], @Quantity::int[],
                        @PerTurn::int[], @DrawType::smallint[], @Spin::int[], @Stop::int[], @MinM::int[], @MaxM::int[])
                 AS x(sort_order, name_vi, name_zh_tw, name_zh_cn, name_en, quantity, draw_per_turn, draw_type,
                      spin_seconds, stop_seconds, min_m, max_m)";
        return await conn.ExecuteAsync(sql, new
        {
            eventId,
            Sort = rows.Select(r => r.SortOrder).ToArray(),
            NameVi = rows.Select(r => r.NameVi).ToArray(),
            NameZhTw = rows.Select(r => r.NameZhTw).ToArray(),
            NameZhCn = rows.Select(r => r.NameZhCn).ToArray(),
            NameEn = rows.Select(r => r.NameEn).ToArray(),
            Quantity = rows.Select(r => r.Quantity).ToArray(),
            PerTurn = rows.Select(r => r.DrawPerTurn).ToArray(),
            DrawType = rows.Select(r => r.DrawType).ToArray(),
            Spin = rows.Select(r => r.SpinSeconds).ToArray(),
            Stop = rows.Select(r => r.StopSeconds).ToArray(),
            MinM = rows.Select(r => r.MinSeniorityMonths).ToArray(),
            MaxM = rows.Select(r => r.MaxSeniorityMonths).ToArray()
        }, tx);
    }
}
