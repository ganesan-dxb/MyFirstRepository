namespace StudentApp.Shared.Messaging1;

// ── Command ───────────────────────────────────────────────────────────────────
// Published by API → consumed by Worker
// A "command" means: please do this thing
public record RegisterStudentCommand
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
    public string FirstName   { get; init; } = string.Empty;
    public string LastName    { get; init; } = string.Empty;
    public string Email       { get; init; } = string.Empty;
    public DateTime DateOfBirth { get; init; }
    public string Grade       { get; init; } = string.Empty;
    public DateTime IssuedAt  { get; init; } = DateTime.UtcNow;
}

// ── Events ────────────────────────────────────────────────────────────────────
// Published by Worker → consumed by Notifications service
// An "event" means: something happened

public record StudentRegisteredEvent
{
    public Guid   CorrelationId { get; init; }
    public int    StudentId     { get; init; }
    public string FullName      { get; init; } = string.Empty;
    public DateTime RegisteredAt { get; init; } = DateTime.UtcNow;
}

public record StudentRegistrationFailedEvent
{
    public Guid   CorrelationId { get; init; }
    public string Reason        { get; init; } = string.Empty;
    public DateTime FailedAt    { get; init; } = DateTime.UtcNow;
}

// ── Registration status ───────────────────────────────────────────────────────
// Stored in Redis, polled by the browser as fallback to SignalR
public class RegistrationStatus
{
    public Guid   CorrelationId { get; set; }
    public string Status        { get; set; } = "Pending";   // Pending | Processing | Done | Failed
    public string? Message      { get; set; }
    public int?   StudentId     { get; set; }
    public DateTime UpdatedAt   { get; set; } = DateTime.UtcNow;
}
