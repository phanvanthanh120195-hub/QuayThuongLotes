using Dapper;
using LuckyDraw.Web.Models.Entities;
using Npgsql;

namespace LuckyDraw.Web.Data.Repositories;

/// <summary>Nhật ký chỉ được thêm và đọc (role luckydraw_app không có quyền UPDATE/DELETE).</summary>
public class AuditRepository
{
    private readonly DbConnectionFactory _db;
    public AuditRepository(DbConnectionFactory db) => _db = db;

    private const string InsertSql = @"
        INSERT INTO luckydraw.audit_log (event_id, action, entity, entity_id, detail, user_name, ip_address)
        VALUES (@EventId, @Action, @Entity, @EntityId, @Detail::jsonb, @UserName, @IpAddress)";

    public async Task InsertAsync(AuditLog log)
    {
        await using var conn = await _db.OpenAsync();
        await conn.ExecuteAsync(InsertSql, log);
    }

    /// <summary>Ghi nhật ký trong cùng transaction với thao tác chính.</summary>
    public Task InsertAsync(AuditLog log, NpgsqlConnection conn, NpgsqlTransaction tx)
        => conn.ExecuteAsync(InsertSql, log, tx);

    public async Task<PagedList<AuditLog>> PageAsync(int? eventId, string? action, int page, int pageSize = 50)
    {
        page = Math.Max(1, page);
        const string sql = @"
            SELECT audit_id, event_id, action, entity, entity_id, detail::text AS detail, user_name, ip_address, created_at,
                   count(*) OVER () AS total_count
            FROM luckydraw.audit_log
            WHERE (@eventId::int IS NULL OR event_id = @eventId)
              AND (@action = '' OR action = @action)
            ORDER BY created_at DESC, audit_id DESC
            LIMIT @pageSize OFFSET @offset";
        await using var conn = await _db.OpenAsync();
        var rows = (await conn.QueryAsync<AuditRow>(sql,
            new { eventId, action = action ?? "", pageSize, offset = (page - 1) * pageSize })).AsList();
        return new PagedList<AuditLog>
        {
            Items = rows,
            Page = page,
            PageSize = pageSize,
            Total = rows.Count > 0 ? (int)rows[0].TotalCount : 0
        };
    }

    public async Task<IReadOnlyList<string>> ActionsAsync()
    {
        await using var conn = await _db.OpenAsync();
        return (await conn.QueryAsync<string>("SELECT DISTINCT action FROM luckydraw.audit_log ORDER BY action")).AsList();
    }

    private sealed class AuditRow : AuditLog
    {
        public long TotalCount { get; set; }
    }
}
