using StudentApp.Gateway.RateLimiting;

namespace StudentApp.Gateway.Middleware;

// Sits in front of YARP and checks Redis before forwarding each request.
// Rules (mirrors the old AspNetCoreRateLimit config):
//   POST /api/registration  → 10 req/min per IP
//   POST /api/students      → 10 req/min per IP
//   DELETE /api/students/*  → 5  req/min per IP
//   Everything else         → 60 req/min per IP
public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RedisRateLimitStore _store;
    private readonly ILogger<RateLimitMiddleware> _logger;

    // (bucket-name, window-seconds, limit)
    private static readonly List<(string Method, string PathPrefix, string Bucket, int Window, int Limit)> Rules =
    [
        ("POST",   "/api/registration", "post-registration", 60, 10),
        ("POST",   "/api/students",     "post-students",     60, 10),
        ("DELETE", "/api/students",     "delete-students",   60,  5),
    ];

    private const int DefaultWindow = 60;
    private const int DefaultLimit  = 60;

    public RateLimitMiddleware(RequestDelegate next, RedisRateLimitStore store, ILogger<RateLimitMiddleware> logger)
    {
        _next   = next;
        _store  = store;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var ip     = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var method = context.Request.Method.ToUpperInvariant();
        var path   = context.Request.Path.Value ?? "/";

        // Find the most specific matching rule
        var rule = Rules.FirstOrDefault(r =>
            r.Method == method && path.StartsWith(r.PathPrefix, StringComparison.OrdinalIgnoreCase));

        var (bucket, window, limit) = rule != default
            ? (rule.Bucket, rule.Window, rule.Limit)
            : ("global", DefaultWindow, DefaultLimit);

        var allowed = await _store.IsAllowedAsync(ip, bucket, window, limit);

        if (!allowed)
        {
            _logger.LogWarning("Rate limit exceeded: IP={Ip} Bucket={Bucket}", ip, bucket);
            context.Response.StatusCode  = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] = window.ToString();
            await context.Response.WriteAsJsonAsync(new
            {
                error   = "Too many requests",
                retryAfterSeconds = window
            });
            return;
        }

        await _next(context);
    }
}

public static class RateLimitMiddlewareExtensions
{
    public static IApplicationBuilder UseRedisRateLimiting(this IApplicationBuilder app)
        => app.UseMiddleware<RateLimitMiddleware>();
}
