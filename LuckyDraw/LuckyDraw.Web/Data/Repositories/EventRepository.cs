using Dapper;
using LuckyDraw.Web.Models.Entities;

namespace LuckyDraw.Web.Data.Repositories;

public class EventRepository
{
    private readonly DbConnectionFactory _db;
    public EventRepository(DbConnectionFactory db) => _db = db;

    private const string Columns = @"e.event_id, e.name_vi, e.name_zh_tw, e.name_zh_cn, e.name_en, e.event_date,
        e.location, e.is_active, e.theme_json::text AS theme_json, e.sound_json::text AS sound_json, e.created_at, e.updated_at";

    public async Task<IReadOnlyList<EventSummary>> ListAsync()
    {
        const string sql = $@"
            SELECT {Columns},
                   (SELECT count(*) FROM luckydraw.prize p WHERE p.event_id = e.event_id)::int AS prize_count,
                   (SELECT coalesce(sum(p.quantity), 0) FROM luckydraw.prize p WHERE p.event_id = e.event_id AND p.is_active)::int AS total_quantity,
                   (SELECT count(*) FROM luckydraw.participant pa WHERE pa.event_id = e.event_id)::int AS participant_count,
                   (SELECT count(*) FROM luckydraw.draw_result r WHERE r.event_id = e.event_id AND NOT r.is_removed)::int AS winner_count
            FROM luckydraw.event e
            ORDER BY e.is_active DESC, e.event_date DESC, e.event_id DESC";
        await using var conn = await _db.OpenAsync();
        return (await conn.QueryAsync<EventSummary>(sql)).AsList();
    }

    public async Task<Event?> GetAsync(int eventId)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QuerySingleOrDefaultAsync<Event>(
            $"SELECT {Columns} FROM luckydraw.event e WHERE e.event_id = @eventId", new { eventId });
    }

    public async Task<Event?> GetActiveAsync()
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QuerySingleOrDefaultAsync<Event>(
            $"SELECT {Columns} FROM luckydraw.event e WHERE e.is_active");
    }

    /// <summary>Sự kiện mặc định cho Admin: sự kiện đang chiếu, không có thì sự kiện mới nhất.</summary>
    public async Task<Event?> GetDefaultAsync()
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QueryFirstOrDefaultAsync<Event>(
            $"SELECT {Columns} FROM luckydraw.event e ORDER BY e.is_active DESC, e.event_date DESC, e.event_id DESC LIMIT 1");
    }

    public async Task<IReadOnlyList<Event>> ListBasicAsync()
    {
        await using var conn = await _db.OpenAsync();
        return (await conn.QueryAsync<Event>(
            $"SELECT {Columns} FROM luckydraw.event e ORDER BY e.is_active DESC, e.event_date DESC, e.event_id DESC")).AsList();
    }

    public async Task<int> InsertAsync(Event e)
    {
        const string sql = @"
            INSERT INTO luckydraw.event (name_vi, name_zh_tw, name_zh_cn, name_en, event_date, location, theme_json, sound_json)
            VALUES (@NameVi, @NameZhTw, @NameZhCn, @NameEn, @EventDate::date, @Location, @ThemeJson::jsonb, @SoundJson::jsonb)
            RETURNING event_id";
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteScalarAsync<int>(sql, e);
    }

    public async Task<bool> UpdateAsync(Event e)
    {
        const string sql = @"
            UPDATE luckydraw.event
               SET name_vi = @NameVi, name_zh_tw = @NameZhTw, name_zh_cn = @NameZhCn, name_en = @NameEn,
                   event_date = @EventDate::date, location = @Location, updated_at = now()
             WHERE event_id = @EventId";
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteAsync(sql, e) == 1;
    }

    public async Task<bool> UpdateThemeAsync(int eventId, string themeJson)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteAsync(
            "UPDATE luckydraw.event SET theme_json = @themeJson::jsonb, updated_at = now() WHERE event_id = @eventId",
            new { eventId, themeJson }) == 1;
    }

    public async Task<bool> UpdateSoundAsync(int eventId, string soundJson)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteAsync(
            "UPDATE luckydraw.event SET sound_json = @soundJson::jsonb, updated_at = now() WHERE event_id = @eventId",
            new { eventId, soundJson }) == 1;
    }

    /// <summary>Đặt sự kiện đang chiếu: tắt sự kiện cũ rồi bật sự kiện mới trong cùng transaction.</summary>
    public async Task<bool> SetActiveAsync(int eventId)
    {
        await using var conn = await _db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        await conn.ExecuteAsync("UPDATE luckydraw.event SET is_active = FALSE, updated_at = now() WHERE is_active AND event_id <> @eventId",
            new { eventId }, tx);
        var n = await conn.ExecuteAsync("UPDATE luckydraw.event SET is_active = TRUE, updated_at = now() WHERE event_id = @eventId",
            new { eventId }, tx);
        if (n != 1) { await tx.RollbackAsync(); return false; }
        await tx.CommitAsync();
        return true;
    }

    public async Task<bool> DeactivateAsync(int eventId)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteAsync(
            "UPDATE luckydraw.event SET is_active = FALSE, updated_at = now() WHERE event_id = @eventId AND is_active",
            new { eventId }) == 1;
    }

    /// <summary>Xoá sự kiện (giải và người tham dự xoá theo). Có kết quả quay thì DB chặn (FK) → ném PostgresException 23503.</summary>
    public async Task<bool> DeleteAsync(int eventId)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteAsync("DELETE FROM luckydraw.event WHERE event_id = @eventId", new { eventId }) == 1;
    }

    public async Task<int> CountResultsAsync(int eventId)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM luckydraw.draw_result WHERE event_id = @eventId", new { eventId });
    }
}
