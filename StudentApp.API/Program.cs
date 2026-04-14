using MassTransit;
using Microsoft.Data.SqlClient;
using StudentApp.API.Services;
using StackExchange.Redis;
using System.Data;
//using HealthChecks.UI.Client;

var builder = WebApplication.CreateBuilder(args);

var sqlConnString = builder.Configuration.GetConnectionString("StudentDb")
                   ?? "Server=localhost,1433;Database=StudentAppDb;User Id=sa;Password=YourStr0ng!Pass;TrustServerCertificate=True";
var redisConn     = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
var rabbitHost    = builder.Configuration["RabbitMQ:Host"]     ?? "localhost";
var rabbitUser    = builder.Configuration["RabbitMQ:Username"] ?? "guest";
var rabbitPass    = builder.Configuration["RabbitMQ:Password"] ?? "guest";

// ──────────────────────────────────────────────────────────────────────────────
// CORS — same allowed origins as Gateway
// The API sits behind the Gateway, but during local dev the browser sometimes
// hits the API directly (e.g. Swagger). Keep in sync with Gateway appsettings.
// ──────────────────────────────────────────────────────────────────────────────
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

// ──────────────────────────────────────────────────────────────────────────────
// Redis
// ──────────────────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConn));

// ──────────────────────────────────────────────────────────────────────────────
// Dapper SQL connection
// ──────────────────────────────────────────────────────────────────────────────
builder.Services.AddTransient<IDbConnection>(_ => new SqlConnection(sqlConnString));

// ──────────────────────────────────────────────────────────────────────────────
// MassTransit
// ──────────────────────────────────────────────────────────────────────────────
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((_, cfg) =>
    {
        cfg.Host(rabbitHost, h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });

        // Messages published from API must be durable
        cfg.ConfigureSend(pipe =>
            pipe.UseSendExecute(ctx => ctx.Durable = true));
    });
});

// ──────────────────────────────────────────────────────────────────────────────
// Services
// ──────────────────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHealthChecks()
    .AddSqlServer(sqlConnString, name: "sqlserver")
    .AddRedis(redisConn, name: "redis");

var app = builder.Build();

// ── Middleware order matters ──────────────────────────────────────────────────
app.UseCors("WebUiPolicy");      // ← MUST be before UseRouting / MapControllers

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
