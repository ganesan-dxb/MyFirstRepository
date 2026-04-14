using MassTransit;
using StudentApp.Shared.Messaging;
using StudentApp.Worker.Services;

namespace StudentApp.Worker.Consumers;

/// <summary>
/// Called by MassTransit when ALL retries and delayed redeliveries are exhausted.
/// At this point the message is in the dead-letter queue.
/// 
/// Responsibilities:
///   1. Update Redis so polling fallback also shows failure
///   2. Publish StudentRegistrationFailedEvent → Notifications → SignalR → Browser
///   3. Log for alerting / monitoring
/// </summary>
public class RegisterStudentFaultConsumer : IConsumer<Fault<RegisterStudentCommand>>
{
    private readonly IRegistrationStatusService           _statusService;
    private readonly ILogger<RegisterStudentFaultConsumer> _logger;

    public RegisterStudentFaultConsumer(
        IRegistrationStatusService statusService,
        ILogger<RegisterStudentFaultConsumer> logger)
    {
        _statusService = statusService;
        _logger        = logger;
    }

    public async Task Consume(ConsumeContext<Fault<RegisterStudentCommand>> context)
    {
        var correlationId = context.Message.Message.CorrelationId;
        var studentName   = context.Message.Message.Name;
        var exceptions    = context.Message.Exceptions;

        var primaryError  = exceptions.FirstOrDefault()?.Message ?? "Unknown error";

        _logger.LogError(
            "[Worker-Fault] All retries exhausted for CorrelationId={CorrelationId}. " +
            "Student={Name}. Error={Error}",
            correlationId, studentName, primaryError);

        // ── Update Redis so polling fallback shows error ──────────────────
        await _statusService.UpdateStatusAsync(
            correlationId,
            RegistrationStatus.Failed,
            "Registration could not be completed after multiple attempts. Please try again.");

        // ── Publish failed event → NotificationConsumer → SignalR → Browser
        await context.Publish(new StudentRegistrationFailedEvent
        {
            CorrelationId = correlationId,
            StudentName   = studentName,
            Reason        = "Service temporarily unavailable. Please try again in a few minutes.",
            FailedAt      = DateTime.UtcNow
        });

        // ── TODO: send to alerting system (PagerDuty, Slack, etc.) ────────
        // await _alertService.SendAsync($"DLQ: Student registration failed for {correlationId}");
    }
}
