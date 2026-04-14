using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace StudentApp.Worker.Services;

/// <summary>
/// Runs every 5 seconds in the background.
/// Checks SQL Server + RabbitMQ health and writes current status to Redis.
/// 
/// The API's /api/system/health endpoint reads from Redis (fast, no DB call).
/// The Web UI polls this every 10s to show a warning banner when services are degraded.
/// </summary>
public class WorkerHealthMonitor : BackgroundService
{
    private readonly HealthCheckService        _healthCheck;
    private readonly IConnectionMultiplexer    _redis;
    private readonly ILogger<WorkerHealthMonitor> _logger;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RedisTtl      = TimeSpan.FromSeconds(30); // expire if worker crashes

    public WorkerHealthMonitor(
        HealthCheckService healthCheck,
        IConnectionMultiplexer redis,
        ILogger<WorkerHealthMonitor> logger)
    {
        _healthCheck = healthCheck;
        _redis       = redis;
        _logger      = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[HealthMonitor] Starting health monitoring loop");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var report = await _healthCheck.CheckHealthAsync(stoppingToken);
                var db     = _redis.GetDatabase();

                foreach (var (name, entry) in report.Entries)
                {
                    var payload = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        Status      = entry.Status.ToString(),
                        Description = entry.Description,
                        CheckedAt   = DateTime.UtcNow
                    });

                    await db.StringSetAsync(
                        $"health:worker:{name}",
                        payload,
                        RedisTtl);
                }

                // Also write overall worker status — if worker is running, it writes this.
                // If worker crashes/goes down, this key expires (TTL) → API knows worker is down.
                await db.StringSetAsync(
                    "health:worker:status",
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        Status    = report.Status.ToString(),
                        CheckedAt = DateTime.UtcNow
                    }),
                    RedisTtl);

                if (report.Status != HealthStatus.Healthy)
                {
                    _logger.LogWarning(
                        "[HealthMonitor] Overall status: {Status}. Details: {Details}",
                        report.Status,
                        string.Join(", ", report.Entries.Select(e => $"{e.Key}={e.Value.Status}")));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[HealthMonitor] Error during health check");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }
}
