using StackExchange.Redis;
using StudentApp.Gateway.Middleware;
using StudentApp.Gateway.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ── Redis ─────────────────────────────────────────────────────────────────────
var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "redis:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConnection));

// ── Rate limiting (Redis-backed sliding window) ───────────────────────────────
builder.Services.AddSingleton<RedisRateLimitStore>();

// ── YARP Reverse Proxy ────────────────────────────────────────────────────────
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// builder.Services.AddCors(options =>
// {
//     options.AddPolicy("GatewayPolicy", policy =>
//     {
//         policy.WithOrigins(
//             builder.Configuration["AllowedOrigins:WebUI"] ?? "http://localhost:5000")
//             .AllowAnyMethod()
//             .AllowAnyHeader()
//             .AllowCredentials();   // needed for SignalR WebSocket upgrade
//     });
// });

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?? new[]
    {
        "http://studentwebapp.localtest.me",
        "https://studentwebapp.localtest.me",
        "http://localhost:5000",
        "https://localhost:5001",
        "http://localhost:5200"  // Gateway itself (for proxied requests)
    };

builder.Services.AddCors(options =>
{
    options.AddPolicy("WebUiPolicy", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

var app = builder.Build();

//app.UseCors("GatewayPolicy");
app.UseCors("WebUiPolicy");

// Rate limiting runs before YARP forwards the request
app.UseRedisRateLimiting();

// YARP handles all proxying
app.MapReverseProxy();

app.Run();
