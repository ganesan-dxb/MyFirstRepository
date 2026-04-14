using MassTransit;
using StudentApp.Shared.Messaging;
using StackExchange.Redis;

namespace StudentApp.API.Services;

public interface IRegistrationService
{
    Task<RegistrationResult> RegisterAsync(RegisterStudentRequest request);
    Task<RegistrationStatusResponse> GetStatusAsync(string correlationId);
    Task<SystemHealthResponse> GetSystemHealthAsync();
}

public class RegistrationService : IRegistrationService
{
    private readonly IPublishEndpoint       _publish;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RegistrationService> _logger;

    private static readonly TimeSpan StatusTtl = TimeSpan.FromHours(24);

    public RegistrationService(
        IPublishEndpoint publish,
        IConnectionMultiplexer redis,
        ILogger<RegistrationService> logger)
    {
        _publish = publish;
        _redis   = redis;
        _logger  = logger;
    }

    // ── Submit registration ───────────────────────────────────────────────────
    public async Task<RegistrationResult> RegisterAsync(RegisterStudentRequest request)
    {
        // Check if worker is alive before accepting — fast fail to user
        var workerHealth = await GetWorkerHealthFromRedisAsync();
        if (workerHealth == "Unhealthy")
        {
            _logger.LogWarning("[API] Worker is Unhealthy — returning ServiceUnavailable");
            return new RegistrationResult
            {
                CorrelationId = string.Empty,
                Accepted      = false,
                Message       = "Registration service is temporarily unavailable. " +
                                "Please try again in a few minutes."
            };
        }

        var correlationId = Guid.NewGuid().ToString();

        // Set initial status in Redis immediately
        await SetStatusInRedisAsync(correlationId, RegistrationStatus.Pending,
            "Your registration has been received and is being processed.");

        // Publish command to RabbitMQ
        // RabbitMQ holds this durably until Worker picks it up
        await _publish.Publish(new RegisterStudentCommand
        {
            CorrelationId = correlationId,
            Name          = request.Name,
            Email         = request.Email,
            Course        = request.Course,
            IssuedAt      = DateTime.UtcNow
        });

        _logger.LogInformation(
            "[API] Published RegisterStudentCommand for CorrelationId={CorrelationId}", correlationId);

        return new RegistrationResult
        {
            CorrelationId = correlationId,
            Accepted      = true,
            Message       = "Registration received. You will be notified shortly."
        };
    }

    // ── Poll status (fallback when SignalR not connected) ─────────────────────
    public async Task<RegistrationStatusResponse> GetStatusAsync(string correlationId)
    {
        var db  = _redis.GetDatabase();
        var raw = await db.StringGetAsync($"registration:{correlationId}");

        if (raw.IsNullOrEmpty)
            return new RegistrationStatusResponse
            {
                CorrelationId = correlationId,
                Status        = RegistrationStatus.Unknown,
                Message       = "No registration found with this ID."
            };

        try
        {
            using var doc    = System.Text.Json.JsonDocument.Parse(raw.ToString()!);
            var root         = doc.RootElement;
            var status       = Enum.Parse<RegistrationStatus>(root.GetProperty("Status").GetString()!);
            var message      = root.GetProperty("Message").GetString();

            return new RegistrationStatusResponse
            {
                CorrelationId = correlationId,
                Status        = status,
                Message       = message
            };
        }
        catch
        {
            return new RegistrationStatusResponse
            {
                CorrelationId = correlationId,
                Status        = RegistrationStatus.Unknown,
                Message       = "Unable to determine status."
            };
        }
    }

    // ── System health (read from Redis — Worker writes this every 5s) ─────────
    public async Task<SystemHealthResponse> GetSystemHealthAsync()
    {
        var db = _redis.GetDatabase();

        var workerStatus = await db.StringGetAsync("health:worker:status");
        var sqlStatus    = await db.StringGetAsync("health:worker:sqlserver");
        var rabbitStatus = await db.StringGetAsync("health:worker:rabbitmq");

        return new SystemHealthResponse
        {
            WorkerStatus   = ParseHealthStatus(workerStatus),
            DatabaseStatus = ParseHealthStatus(sqlStatus),
            QueueStatus    = ParseHealthStatus(rabbitStatus),
            CheckedAt      = DateTime.UtcNow
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private async Task<string> GetWorkerHealthFromRedisAsync()
    {
        var db  = _redis.GetDatabase();
        var raw = await db.StringGetAsync("health:worker:status");
        if (raw.IsNullOrEmpty) return "Unhealthy"; // key expired = worker is down
        return ParseHealthStatus(raw);
    }

    private static string ParseHealthStatus(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return "Unknown";
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(raw);
            return doc.RootElement.GetProperty("Status").GetString() ?? "Unknown";
        }
        catch { return "Unknown"; }
    }

    private async Task SetStatusInRedisAsync(
        string correlationId, RegistrationStatus status, string message)
    {
        var db      = _redis.GetDatabase();
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            Status    = status.ToString(),
            Message   = message,
            UpdatedAt = DateTime.UtcNow
        });
        await db.StringSetAsync(
            $"registration:{correlationId}",
            payload,
            StatusTtl);
    }
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public class RegisterStudentRequest
{
    public string Name   { get; set; } = string.Empty;
    public string Email  { get; set; } = string.Empty;
    public string Course { get; set; } = string.Empty;
}

public class RegistrationResult
{
    public string CorrelationId { get; set; } = string.Empty;
    public bool   Accepted      { get; set; }
    public string Message       { get; set; } = string.Empty;
}

public class RegistrationStatusResponse
{
    public string             CorrelationId { get; set; } = string.Empty;
    public RegistrationStatus Status        { get; set; }
    public string?            Message       { get; set; }
}

public class SystemHealthResponse
{
    public string   WorkerStatus   { get; set; } = string.Empty;
    public string   DatabaseStatus { get; set; } = string.Empty;
    public string   QueueStatus    { get; set; } = string.Empty;
    public DateTime CheckedAt      { get; set; }
}
