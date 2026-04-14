using MassTransit;
using StudentApp.Shared.Messaging;
using StackExchange.Redis;

namespace StudentApp.Worker.Services;

// ──────────────────────────────────────────────────────────────────────────────
// Interface
// ──────────────────────────────────────────────────────────────────────────────
public interface IRegistrationStatusService
{
    Task UpdateStatusAsync(string correlationId, RegistrationStatus status, string? message = null);
    Task<(RegistrationStatus Status, string? Message)> GetStatusAsync(string correlationId);
}

// ──────────────────────────────────────────────────────────────────────────────
// Implementation
// Writes status to Redis (for polling fallback) AND publishes SignalR event
// ──────────────────────────────────────────────────────────────────────────────
public class RegistrationStatusService : IRegistrationStatusService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IPublishEndpoint       _publish;
    private readonly ILogger<RegistrationStatusService> _logger;

    // How long to keep status in Redis
    private static readonly TimeSpan StatusTtl = TimeSpan.FromHours(24);

    public RegistrationStatusService(
        IConnectionMultiplexer redis,
        IPublishEndpoint publish,
        ILogger<RegistrationStatusService> logger)
    {
        _redis   = redis;
        _publish = publish;
        _logger  = logger;
    }

    public async Task UpdateStatusAsync(
        string correlationId,
        RegistrationStatus status,
        string? message = null)
    {
        var db = _redis.GetDatabase();

        // Store as JSON so we can also store the message
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

        _logger.LogInformation(
            "[StatusService] CorrelationId={CorrelationId} → {Status}: {Message}",
            correlationId, status, message);

        // Also publish a status-update event so Notifications can push via SignalR
        // (separate from StudentRegisteredEvent / StudentRegistrationFailedEvent)
        if (status == RegistrationStatus.Retrying)
        {
            await _publish.Publish(new RegistrationStatusUpdatedEvent
            {
                CorrelationId = correlationId,
                Status        = status,
                Message       = message ?? string.Empty,
                UpdatedAt     = DateTime.UtcNow
            });
        }
    }

    public async Task<(RegistrationStatus Status, string? Message)> GetStatusAsync(string correlationId)
    {
        var db  = _redis.GetDatabase();
        var raw = await db.StringGetAsync($"registration:{correlationId}");

        if (raw.IsNullOrEmpty)
            return (RegistrationStatus.Unknown, null);

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(raw.ToString()!);
            var root   = doc.RootElement;
            var status  = Enum.Parse<RegistrationStatus>(root.GetProperty("Status").GetString()!);
            var message = root.GetProperty("Message").GetString();
            return (status, message);
        }
        catch
        {
            return (RegistrationStatus.Unknown, null);
        }
    }
}
