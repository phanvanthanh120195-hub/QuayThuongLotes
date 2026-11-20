using Dapper;
using LuckyDraw.Web.Models.Entities;
using Npgsql;

namespace LuckyDraw.Web.Data.Repositories;

public class ParticipantSearch
{
    public string? Q { get; set; }
    public int? DepartmentId { get; set; }
    /// <summary>Lọc theo tên bộ phận (cột Bộ phận của người tham dự).</summary>
    public string? Dept { get; set; }
    /// <summary>null = tất cả | eligible = chưa trúng & có mặt | won = đã trúng | absent = vắng mặt</summary>
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class ParticipantStats
{
    public int Total { get; set; }
    public int Absent { get; set; }
    public int Won { get; set; }
}

public class ParticipantRepository
{
    private readonly DbConnectionFactory _db;
    public ParticipantRepository(DbConnectionFactory db) => _db = db;

    public static string LikePattern(string? q)
    {
        if (string.IsNullOrWhiteSpace(q)) return "";
        var escaped = q.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        return "%" + escaped + "%";
    }

    public async Task<PagedList<ParticipantRow>> SearchAsync(int eventId, ParticipantSearch s)
    {
        var page = Math.Max(1, s.Page);
        var size = Math.Clamp(s.PageSize, 10, 500);
        const string sql = @"
            SELECT p.participant_id, p.event_id, p.employee_code, p.full_name, p.full_name_zh, p.department_vi,
                   p.department_zh, p.position, p.photo_path, p.department_id, p.join_date, p.is_absent,
                   d.code AS department_code,
                   COALESCE(p.department_vi, d.name_vi) AS department_name_vi,
                   COALESCE(p.department_zh, d.name_zh_tw) AS department_name_zh_tw,
                   COALESCE(p.department_zh, d.name_zh_cn) AS department_name_zh_cn,
                   CASE WHEN p.department_vi IS NULL AND p.department_zh IS NULL THEN d.name_en END AS department_name_en,
                   z.prize_id AS won_prize_id, z.name_vi AS won_prize_name_vi, z.name_zh_tw AS won_prize_name_zh_tw,
                   z.name_zh_cn AS won_prize_name_zh_cn, z.name_en AS won_prize_name_en,
                   count(*) OVER () AS total_count
            FROM luckydraw.participant p
            LEFT JOIN luckydraw.department d ON d.department_id = p.department_id
            LEFT JOIN luckydraw.draw_result r ON r.event_id = p.event_id AND r.participant_id = p.participant_id AND NOT r.is_removed
            LEFT JOIN luckydraw.prize z ON z.prize_id = r.prize_id
            WHERE p.event_id = @eventId
              AND (@q = '' OR p.employee_code ILIKE @q OR p.full_name ILIKE @q OR p.full_name_zh ILIKE @q
                   OR p.department_vi ILIKE @q OR p.department_zh ILIKE @q OR p.position ILIKE @q)
              AND (@deptId::int IS NULL OR p.department_id = @deptId)
              AND (@dept = '' OR COALESCE(p.department_vi, d.name_vi) = @dept)
              AND (@status = ''
                   OR (@status = 'won' AND r.result_id IS NOT NULL)
                   OR (@status = 'absent' AND p.is_absent)
                   OR (@status = 'eligible' AND NOT p.is_absent AND r.result_id IS NULL))
            ORDER BY p.employee_code
            LIMIT @size OFFSET @offset";
        await using var conn = await _db.OpenAsync();
        var rows = (await conn.QueryAsync<ParticipantRow>(sql, new
        {
            eventId,
            q = LikePattern(s.Q),
            deptId = s.DepartmentId,
            dept = s.Dept?.Trim() ?? "",
            status = s.Status ?? "",
            size,
            offset = (page - 1) * size
        })).AsList();
        return new PagedList<ParticipantRow>
        {
            Items = rows,
            Page = page,
            PageSize = size,
            Total = rows.Count > 0 ? (int)rows[0].TotalCount : 0
        };
    }

    public async Task<ParticipantStats> StatsAsync(int eventId)
    {
        const string sql = @"
            SELECT count(*)::int AS total,
                   count(*) FILTER (WHERE p.is_absent)::int AS absent,
                   (SELECT count(*) FROM luckydraw.draw_result r WHERE r.event_id = @eventId AND NOT r.is_removed)::int AS won
            FROM luckydraw.participant p
            WHERE p.event_id = @eventId";
        await using var conn = await _db.OpenAsync();
        return await conn.QuerySingleAsync<ParticipantStats>(sql, new { eventId });
    }

    /// <summary>Mã nhân viên của sự kiện (dùng để dựng ô quay số: số ô và ký tự có thể xuất hiện ở mỗi ô).</summary>
    public async Task<IReadOnlyList<string>> CodesAsync(int eventId)
    {
        await using var conn = await _db.OpenAsync();
        return (await conn.QueryAsync<string>(
            "SELECT employee_code FROM luckydraw.participant WHERE event_id = @eventId", new { eventId })).AsList();
    }

    /// <summary>Các tên bộ phận (Việt / Trung) đang có trong sự kiện – gợi ý khi nhập và bộ lọc danh sách.</summary>
    public async Task<(IReadOnlyList<string> Vi, IReadOnlyList<string> Zh)> DepartmentNamesAsync(int eventId)
    {
        await using var conn = await _db.OpenAsync();
        var vi = (await conn.QueryAsync<string>(@"
            SELECT DISTINCT department_vi FROM luckydraw.participant
            WHERE event_id = @eventId AND department_vi IS NOT NULL ORDER BY 1", new { eventId })).AsList();
        var zh = (await conn.QueryAsync<string>(@"
            SELECT DISTINCT department_zh FROM luckydraw.participant
            WHERE event_id = @eventId AND department_zh IS NOT NULL ORDER BY 1", new { eventId })).AsList();
        return (vi, zh);
    }

    public async Task<Participant?> GetAsync(int eventId, int id)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QuerySingleOrDefaultAsync<Participant>(@"
            SELECT participant_id, event_id, employee_code, full_name, full_name_zh, department_vi, department_zh,
                   position, photo_path, department_id, join_date, is_absent
            FROM luckydraw.participant WHERE participant_id = @id AND event_id = @eventId", new { eventId, id });
    }

    public async Task<int> InsertAsync(Participant p)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO luckydraw.participant (event_id, employee_code, full_name, full_name_zh, department_vi,
                   department_zh, position, photo_path, department_id, join_date, is_absent)
            VALUES (@EventId, @EmployeeCode, @FullName, @FullNameZh, @DepartmentVi, @DepartmentZh, @Position,
                   @PhotoPath, @DepartmentId, @JoinDate::date, @IsAbsent)
            RETURNING participant_id", p);
    }

    public async Task<bool> UpdateAsync(Participant p)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteAsync(@"
            UPDATE luckydraw.participant
               SET employee_code = @EmployeeCode, full_name = @FullName, full_name_zh = @FullNameZh,
                   department_vi = @DepartmentVi, department_zh = @DepartmentZh, position = @Position,
                   photo_path = @PhotoPath, department_id = @DepartmentId,
                   join_date = @JoinDate::date, is_absent = @IsAbsent, updated_at = now()
             WHERE participant_id = @ParticipantId AND event_id = @EventId", p) == 1;
    }

    public async Task<bool> SetAbsentAsync(int eventId, int id, bool absent)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteAsync(@"
            UPDATE luckydraw.participant SET is_absent = @absent, updated_at = now()
             WHERE participant_id = @id AND event_id = @eventId", new { eventId, id, absent }) == 1;
    }

    /// <summary>Mã nhân viên → (id, ảnh hiện tại) để ghép ảnh tải hàng loạt theo tên file.</summary>
    public async Task<Dictionary<string, (int Id, string? PhotoPath)>> PhotoMapAsync(int eventId)
    {
        await using var conn = await _db.OpenAsync();
        var rows = await conn.QueryAsync<(int Id, string Code, string? PhotoPath)>(
            "SELECT participant_id, employee_code, photo_path FROM luckydraw.participant WHERE event_id = @eventId",
            new { eventId });
        return rows.ToDictionary(r => r.Code.Trim(), r => (r.Id, r.PhotoPath), StringComparer.OrdinalIgnoreCase);
    }

    public async Task<bool> SetPhotoAsync(int eventId, int id, string? photoPath)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteAsync(@"
            UPDATE luckydraw.participant SET photo_path = @photoPath, updated_at = now()
             WHERE participant_id = @id AND event_id = @eventId", new { eventId, id, photoPath }) == 1;
    }

    /// <summary>Người đã có kết quả quay (kể cả đã xoá mềm) thì DB chặn (FK) → PostgresException 23503.</summary>
    public async Task<bool> DeleteAsync(int eventId, int id)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteAsync("DELETE FROM luckydraw.participant WHERE participant_id = @id AND event_id = @eventId",
            new { eventId, id }) == 1;
    }

    /// <summary>Xoá tất cả người CHƯA từng có kết quả quay. Trả về số dòng đã xoá.</summary>
    /// <returns>Ảnh của những người đã xoá (để xoá file) – số phần tử = số người đã xoá.</returns>
    public async Task<IReadOnlyList<string?>> DeleteAllWithoutResultsAsync(int eventId)
    {
        await using var conn = await _db.OpenAsync();
        return (await conn.QueryAsync<string?>(@"
            DELETE FROM luckydraw.participant p
             WHERE p.event_id = @eventId
               AND NOT EXISTS (SELECT 1 FROM luckydraw.draw_result r WHERE r.participant_id = p.participant_id)
            RETURNING p.photo_path",
            new { eventId })).AsList();
    }

    /// <summary>Import: upsert theo (event_id, employee_code) trong 1 lệnh. Trả về (thêm mới, cập nhật).</summary>
    public async Task<(int Inserted, int Updated)> UpsertManyAsync(int eventId, IReadOnlyList<Participant> rows, bool overwrite,
        NpgsqlConnection conn, NpgsqlTransaction tx)
    {
        var sql = $@"
            INSERT INTO luckydraw.participant (event_id, employee_code, full_name, full_name_zh, department_vi,
                   department_zh, position, department_id, join_date)
            SELECT @eventId, x.code, x.name, x.name_zh, x.dvi, x.dzh, x.pos, x.dept, x.jd
            FROM unnest(@Codes::text[], @Names::text[], @NamesZh::text[], @DeptVi::text[], @DeptZh::text[],
                        @Positions::text[], @Depts::int[], @Dates::date[])
                 AS x(code, name, name_zh, dvi, dzh, pos, dept, jd)
            ON CONFLICT (event_id, employee_code) DO {(overwrite
                ? "UPDATE SET full_name = EXCLUDED.full_name, full_name_zh = EXCLUDED.full_name_zh, department_vi = EXCLUDED.department_vi, department_zh = EXCLUDED.department_zh, position = EXCLUDED.position, department_id = COALESCE(EXCLUDED.department_id, participant.department_id), join_date = EXCLUDED.join_date, updated_at = now()"
                : "NOTHING")}
            RETURNING (xmax = 0) AS inserted";
        var result = (await conn.QueryAsync<bool>(sql, new
        {
            eventId,
            Codes = rows.Select(r => r.EmployeeCode).ToArray(),
            Names = rows.Select(r => r.FullName).ToArray(),
            NamesZh = rows.Select(r => r.FullNameZh).ToArray(),
            DeptVi = rows.Select(r => r.DepartmentVi).ToArray(),
            DeptZh = rows.Select(r => r.DepartmentZh).ToArray(),
            Positions = rows.Select(r => r.Position).ToArray(),
            Depts = rows.Select(r => r.DepartmentId).ToArray(),
            Dates = rows.Select(r => r.JoinDate.Date).ToArray()
        }, tx)).AsList();
        var inserted = result.Count(x => x);
        return (inserted, result.Count - inserted);
    }
}
