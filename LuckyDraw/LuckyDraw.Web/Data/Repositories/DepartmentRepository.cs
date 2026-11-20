using Dapper;
using LuckyDraw.Web.Models.Entities;
using Npgsql;

namespace LuckyDraw.Web.Data.Repositories;

public class DepartmentRepository
{
    private readonly DbConnectionFactory _db;
    public DepartmentRepository(DbConnectionFactory db) => _db = db;

    private const string Columns = "d.department_id, d.code, d.name_vi, d.name_zh_tw, d.name_zh_cn, d.name_en, d.sort_order";

    public async Task<IReadOnlyList<Department>> ListAsync()
    {
        const string sql = $@"
            SELECT {Columns},
                   (SELECT count(*) FROM luckydraw.participant p WHERE p.department_id = d.department_id)::int AS participant_count
            FROM luckydraw.department d
            ORDER BY d.sort_order, d.code";
        await using var conn = await _db.OpenAsync();
        return (await conn.QueryAsync<Department>(sql)).AsList();
    }

    public async Task<Department?> GetAsync(int id)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QuerySingleOrDefaultAsync<Department>(
            $"SELECT {Columns} FROM luckydraw.department d WHERE d.department_id = @id", new { id });
    }

    public async Task<int> InsertAsync(Department d)
    {
        const string sql = @"
            INSERT INTO luckydraw.department (code, name_vi, name_zh_tw, name_zh_cn, name_en, sort_order)
            VALUES (@Code, @NameVi, @NameZhTw, @NameZhCn, @NameEn, @SortOrder)
            RETURNING department_id";
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteScalarAsync<int>(sql, d);
    }

    public async Task<bool> UpdateAsync(Department d)
    {
        const string sql = @"
            UPDATE luckydraw.department
               SET code = @Code, name_vi = @NameVi, name_zh_tw = @NameZhTw, name_zh_cn = @NameZhCn,
                   name_en = @NameEn, sort_order = @SortOrder, updated_at = now()
             WHERE department_id = @DepartmentId";
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteAsync(sql, d) == 1;
    }

    /// <summary>Còn người tham dự thuộc bộ phận thì DB chặn (FK) → PostgresException 23503.</summary>
    public async Task<bool> DeleteAsync(int id)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteAsync("DELETE FROM luckydraw.department WHERE department_id = @id", new { id }) == 1;
    }

    /// <summary>Mã bộ phận (chữ thường) → id, dùng khi import.</summary>
    public async Task<Dictionary<string, int>> CodeMapAsync()
    {
        await using var conn = await _db.OpenAsync();
        var rows = await conn.QueryAsync<(string Code, int Id)>("SELECT code, department_id FROM luckydraw.department");
        return rows.ToDictionary(r => r.Code.ToLowerInvariant(), r => r.Id);
    }

    /// <summary>Import: upsert theo mã trong 1 lệnh. Trả về (thêm mới, cập nhật).</summary>
    public async Task<(int Inserted, int Updated)> UpsertManyAsync(IReadOnlyList<Department> rows, bool overwrite, NpgsqlConnection conn, NpgsqlTransaction tx)
    {
        var sql = $@"
            INSERT INTO luckydraw.department (code, name_vi, name_zh_tw, name_zh_cn, name_en, sort_order)
            SELECT x.code, x.name_vi, x.name_zh_tw, x.name_zh_cn, x.name_en, x.sort_order
            FROM unnest(@Codes::text[], @NameVi::text[], @NameZhTw::text[], @NameZhCn::text[], @NameEn::text[], @Sort::int[])
                 AS x(code, name_vi, name_zh_tw, name_zh_cn, name_en, sort_order)
            ON CONFLICT (code) DO {(overwrite
                ? "UPDATE SET name_vi = EXCLUDED.name_vi, name_zh_tw = EXCLUDED.name_zh_tw, name_zh_cn = EXCLUDED.name_zh_cn, name_en = EXCLUDED.name_en, updated_at = now()"
                : "NOTHING")}
            RETURNING (xmax = 0) AS inserted";
        var result = (await conn.QueryAsync<bool>(sql, new
        {
            Codes = rows.Select(r => r.Code).ToArray(),
            NameVi = rows.Select(r => r.NameVi).ToArray(),
            NameZhTw = rows.Select(r => r.NameZhTw).ToArray(),
            NameZhCn = rows.Select(r => r.NameZhCn).ToArray(),
            NameEn = rows.Select(r => r.NameEn).ToArray(),
            Sort = rows.Select(r => r.SortOrder).ToArray()
        }, tx)).AsList();
        var inserted = result.Count(x => x);
        return (inserted, result.Count - inserted);
    }
}
