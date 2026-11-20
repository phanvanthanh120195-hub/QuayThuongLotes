using Npgsql;

namespace LuckyDraw.Web.Data;

/// <summary>
/// Tạo NpgsqlConnection từ chuỗi kết nối "ConnectionStrings:LuckyDraw".
/// Chuỗi kết nối để trong User Secrets / biến môi trường, KHÔNG để trong appsettings.json.
/// </summary>
public sealed class DbConnectionFactory : IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    public DbConnectionFactory(IConfiguration configuration)
    {
        var cs = configuration.GetConnectionString("LuckyDraw");
        if (string.IsNullOrWhiteSpace(cs))
        {
            throw new InvalidOperationException(
                "Chưa cấu hình ConnectionStrings:LuckyDraw. Chạy: dotnet user-secrets set \"ConnectionStrings:LuckyDraw\" \"Host=...;Password=...\"");
        }
        _dataSource = NpgsqlDataSource.Create(cs);
    }

    public async Task<NpgsqlConnection> OpenAsync(CancellationToken ct = default)
        => await _dataSource.OpenConnectionAsync(ct);

    public ValueTask DisposeAsync() => _dataSource.DisposeAsync();
}

public static class PgErrors
{
    public const string UniqueViolation = PostgresErrorCodes.UniqueViolation;      // 23505
    public const string ForeignKeyViolation = PostgresErrorCodes.ForeignKeyViolation; // 23503
    public const string CheckViolation = PostgresErrorCodes.CheckViolation;        // 23514

    public static bool Is(Exception ex, string sqlState, string? constraint = null)
        => ex is PostgresException pg && pg.SqlState == sqlState
           && (constraint is null || pg.ConstraintName == constraint);
}
