using System.Diagnostics;
using Dapper;
using LuckyDraw.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace LuckyDraw.Web.Controllers.Api;

/// <summary>
/// GET /api/health – màn chiếu gọi định kỳ để biết máy chủ và database còn kết nối không.
/// Không cần đăng nhập, chỉ trả về trạng thái (không có dữ liệu).
/// </summary>
[ApiController]
[Route("api/health")]
public class HealthApiController : ControllerBase
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(4);
    private readonly DbConnectionFactory _db;
    private readonly ILogger<HealthApiController> _log;

    public HealthApiController(DbConnectionFactory db, ILogger<HealthApiController> log)
    {
        _db = db;
        _log = log;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        Response.Headers.CacheControl = "no-store";
        var sw = Stopwatch.StartNew();
        using var cts = new CancellationTokenSource(Timeout);
        try
        {
            await using var conn = await _db.OpenAsync(cts.Token);
            await conn.ExecuteScalarAsync<int>(new CommandDefinition("SELECT 1", cancellationToken: cts.Token));
            return Ok(new { db = true, ms = sw.ElapsedMilliseconds });
        }
        catch (Exception ex) when (DbErrors.IsConnectionError(ex) || ex is OperationCanceledException)
        {
            _log.LogWarning("Health check: database unreachable after {Ms} ms: {Error}", sw.ElapsedMilliseconds, ex.Message);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { db = false, ms = sw.ElapsedMilliseconds });
        }
    }
}

public static class DbErrors
{
    /// <summary>
    /// Lỗi do mất kết nối / quá thời gian tới database (không phải lỗi SQL như vi phạm ràng buộc).
    /// PostgresException = server Postgres trả lỗi SQL → không phải lỗi mạng, trừ khi Npgsql đánh dấu tạm thời.
    /// </summary>
    public static bool IsConnectionError(Exception ex) => ex switch
    {
        PostgresException pg => pg.IsTransient,
        NpgsqlException => true,
        TimeoutException => true,
        System.Net.Sockets.SocketException => true,
        IOException => true,
        _ => ex.InnerException != null && IsConnectionError(ex.InnerException)
    };
}
