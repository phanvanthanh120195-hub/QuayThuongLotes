using Dapper;
using LuckyDraw.Web.Models.Entities;

namespace LuckyDraw.Web.Data.Repositories;

public class UserRepository
{
    private readonly DbConnectionFactory _db;
    public UserRepository(DbConnectionFactory db) => _db = db;

    private const string Columns = "user_id, user_name, display_name, password_hash, role, is_active, last_login_at, created_at";

    public async Task<IReadOnlyList<AppUser>> ListAsync()
    {
        await using var conn = await _db.OpenAsync();
        return (await conn.QueryAsync<AppUser>(
            $"SELECT {Columns} FROM luckydraw.app_user ORDER BY role, lower(user_name)")).AsList();
    }

    public async Task<AppUser?> GetAsync(int id)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QuerySingleOrDefaultAsync<AppUser>(
            $"SELECT {Columns} FROM luckydraw.app_user WHERE user_id = @id", new { id });
    }

    public async Task<AppUser?> GetByNameAsync(string userName)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QuerySingleOrDefaultAsync<AppUser>(
            $"SELECT {Columns} FROM luckydraw.app_user WHERE lower(user_name) = lower(@userName)", new { userName });
    }

    public async Task<int> CountAsync()
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteScalarAsync<int>("SELECT count(*) FROM luckydraw.app_user");
    }

    public async Task<int> CountActiveAdminsAsync()
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM luckydraw.app_user WHERE role = 'Admin' AND is_active");
    }

    public async Task<int> InsertAsync(AppUser u)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO luckydraw.app_user (user_name, display_name, password_hash, role, is_active)
            VALUES (@UserName, @DisplayName, @PasswordHash, @Role, @IsActive)
            RETURNING user_id", u);
    }

    public async Task<bool> UpdateAsync(AppUser u)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteAsync(@"
            UPDATE luckydraw.app_user
               SET display_name = @DisplayName, role = @Role, is_active = @IsActive, updated_at = now()
             WHERE user_id = @UserId", u) == 1;
    }

    public async Task<bool> SetPasswordAsync(int id, string hash)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteAsync(
            "UPDATE luckydraw.app_user SET password_hash = @hash, updated_at = now() WHERE user_id = @id",
            new { id, hash }) == 1;
    }

    public async Task TouchLoginAsync(int id)
    {
        await using var conn = await _db.OpenAsync();
        await conn.ExecuteAsync("UPDATE luckydraw.app_user SET last_login_at = now() WHERE user_id = @id", new { id });
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ExecuteAsync("DELETE FROM luckydraw.app_user WHERE user_id = @id", new { id }) == 1;
    }
}
