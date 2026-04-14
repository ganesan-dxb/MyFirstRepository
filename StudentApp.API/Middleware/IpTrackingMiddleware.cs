using StackExchange.Redis;

namespace StudentApp.API.Middleware;

// Tracks every external IP that calls the API.
// Stores in Redis as a sorted set: "ip-tracker:calls" with timestamp scores.
public class IpTrackingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IpTrackingMiddleware> _logger;
    private readonly IConnectionMultiplexer _redis;

    public IpTrackingMiddleware(RequestDelegate next, ILogger<IpTrackingMiddleware> logger, IConnectionMultiplexer redis)
    {
        _next = next;
        _logger = logger;
        _redis = redis;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var path = context.Request.Path;
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        try
        {
            var db = _redis.GetDatabase();

            // Store: ip|path → sorted set with Unix timestamp score
            // This lets you query "how many calls from IP X in the last N seconds"
            var key = "ip-tracker:calls";
            var member = $"{ip}|{path}|{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}";
            await db.SortedSetAddAsync(key, member, timestamp);

            // Keep only the last 24 hours of data
            var cutoff = timestamp - 86400;
            await db.SortedSetRemoveRangeByScoreAsync(key, double.NegativeInfinity, cutoff);

            // Also track unique IPs in a set
            await db.SetAddAsync("ip-tracker:unique-ips", ip);

            _logger.LogInformation("IP Tracked: {Ip} → {Path}", ip, path);
        }
        catch (Exception ex)
        {
            // Never let tracking break the actual request
            _logger.LogWarning(ex, "IP tracking failed for {Ip}", ip);
        }

        await _next(context);
    }
}

public static class IpTrackingMiddlewareExtensions
{
    public static IApplicationBuilder UseIpTracking(this IApplicationBuilder app)
        => app.UseMiddleware<IpTrackingMiddleware>();
}
