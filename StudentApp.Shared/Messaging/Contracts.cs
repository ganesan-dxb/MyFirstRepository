namespace StudentApp.Shared.Messaging;

// ──────────────────────────────────────────────────────────────────────────────
// Commands (sent from API → RabbitMQ → Worker)
// ──────────────────────────────────────────────────────────────────────────────

public class RegisterStudentCommand
{
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();
    public string Name          { get; init; } = string.Empty;
    public string Email         { get; init; } = string.Empty;
    public string Course        { get; init; } = string.Empty;
    public DateTime IssuedAt    { get; init; } = DateTime.UtcNow;
}

// ──────────────────────────────────────────────────────────────────────────────
// Events (published from Worker → Notifications → SignalR → Browser)
// ──────────────────────────────────────────────────────────────────────────────

public class StudentRegisteredEvent
{
    public string CorrelationId { get; init; } = string.Empty;
    public string StudentName   { get; init; } = string.Empty;
    public DateTime RegisteredAt { get; init; } = DateTime.UtcNow;
}

public class StudentRegistrationFailedEvent
{
    public string CorrelationId { get; init; } = string.Empty;
    public string StudentName   { get; init; } = string.Empty;
    public string Reason        { get; init; } = string.Empty;
    public DateTime FailedAt    { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Published during retry phase so the UI can show intermediate messages
/// (e.g., "Still working...", "DB temporarily unavailable")
/// </summary>
public class RegistrationStatusUpdatedEvent
{
    public string CorrelationId  { get; init; } = string.Empty;
    public RegistrationStatus Status { get; init; }
    public string Message        { get; init; } = string.Empty;
    public DateTime UpdatedAt    { get; init; } = DateTime.UtcNow;
}

// ──────────────────────────────────────────────────────────────────────────────
// Status enum (stored in Redis, sent over SignalR)
// ──────────────────────────────────────────────────────────────────────────────

public enum RegistrationStatus
{
    Unknown    = 0,
    Pending    = 1,   // Published to RabbitMQ, not yet processed
    Processing = 2,   // Worker picked up, DB insert in progress
    Retrying   = 3,   // DB/Worker down, retrying — user should see warning
    Completed  = 4,   // Successfully inserted to DB
    Failed     = 5    // All retries exhausted — user must retry
}
