using Dapper;
using MassTransit;
using Microsoft.Data.SqlClient;
using StudentApp.Shared.Messaging;
using StudentApp.Worker.Services;
using System.Data;

namespace StudentApp.Worker.Consumers;

/// <summary>
/// Consumes RegisterStudentCommand from RabbitMQ.
/// 
/// Retry strategy:
///   Fast retries (3x):  3s → 6s → 9s  (covers transient blips)
///   Delayed redelivery: 10s → 30s → 60s (covers DB restart / Worker restart)
///   After all fail     → Fault → RegisterStudentFaultConsumer → user sees error
///
/// User communication timeline:
///   0s    — "In Progress" shown immediately on form submit (202 Accepted)
///   ~10s  — If DB still down after fast retries, user sees "Still working…" toast
///   ~15s  — If first delayed redelivery also fails, user sees "Taking longer than expected"
///   ~60s+ — If all retries exhausted, user sees "Registration failed. Please try again."
/// </summary>
public class RegisterStudentConsumer : IConsumer<RegisterStudentCommand>
{
    private readonly IDbConnection              _db;
    private readonly IRegistrationStatusService _statusService;
    private readonly ILogger<RegisterStudentConsumer> _logger;

    public RegisterStudentConsumer(
        IDbConnection db,
        IRegistrationStatusService statusService,
        ILogger<RegisterStudentConsumer> logger)
    {
        _db            = db;
        _statusService = statusService;
        _logger        = logger;
    }

    public async Task Consume(ConsumeContext<RegisterStudentCommand> context)
    {
        var cmd           = context.Message;
        var correlationId = cmd.CorrelationId;

        _logger.LogInformation(
            "[Worker] Attempt #{Attempt} for CorrelationId={CorrelationId}",
            context.GetRetryAttempt() + 1, correlationId);

        // ── Notify user after first failure (attempt 1+) ──────────────────
        // GetRetryAttempt() == 0 means this is the FIRST try
        // == 1 means first retry — tell the user we're still trying
        if (context.GetRetryAttempt() == 1)
        {
            await _statusService.UpdateStatusAsync(
                correlationId,
                RegistrationStatus.Retrying,
                "Database is temporarily unavailable. Retrying automatically...");
        }
        else if (context.GetRedeliveryCount() >= 1)
        {
            // Delayed redelivery means it survived fast retries — stronger message
            await _statusService.UpdateStatusAsync(
                correlationId,
                RegistrationStatus.Retrying,
                "Still working on your registration. This is taking longer than expected.");
        }

        try
        {
            // ── Ensure connection is open ──────────────────────────────────
            if (_db.State != ConnectionState.Open)
                _db.Open();

            // ── Idempotency check — don't double-insert on retry ──────────
            var existing = await _db.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Students WHERE CorrelationId = @CorrelationId",
                new { correlationId });

            if (existing > 0)
            {
                _logger.LogWarning(
                    "[Worker] Duplicate detected for CorrelationId={CorrelationId}, skipping insert.",
                    correlationId);

                // Still publish success event — idempotent success
                await context.Publish(new StudentRegisteredEvent
                {
                    CorrelationId = correlationId,
                    StudentName   = cmd.Name
                });
                return;
            }

            // ── Insert student ─────────────────────────────────────────────
            await _db.ExecuteAsync(
                @"INSERT INTO Students (Id, Name, Email, Course, CorrelationId, RegisteredAt)
                  VALUES (@Id, @Name, @Email, @Course, @CorrelationId, @RegisteredAt)",
                new
                {
                    Id            = Guid.NewGuid(),
                    cmd.Name,
                    cmd.Email,
                    cmd.Course,
                    CorrelationId = correlationId,
                    RegisteredAt  = DateTime.UtcNow
                });

            _logger.LogInformation(
                "[Worker] Successfully inserted student for CorrelationId={CorrelationId}", correlationId);

            // ── Publish success event → Notifications → SignalR → Browser ──
            await context.Publish(new StudentRegisteredEvent
            {
                CorrelationId = correlationId,
                StudentName   = cmd.Name
            });
        }
        catch (SqlException ex) when (IsTransientSqlError(ex))
        {
            // Transient: let MassTransit retry — do NOT catch permanently
            _logger.LogWarning(
                "[Worker] Transient SQL error (#{Code}) for CorrelationId={CorrelationId}. " +
                "MassTransit will retry. Message: {Msg}",
                ex.Number, correlationId, ex.Message);

            throw; // ← MassTransit sees this and applies retry policy
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[Worker] Unrecoverable error for CorrelationId={CorrelationId}", correlationId);
            throw; // → goes to FaultConsumer after retries exhausted
        }
        finally
        {
            if (_db.State == ConnectionState.Open)
                _db.Close();
        }
    }

    /// <summary>
    /// Only transient SQL errors should be retried.
    /// Permanent errors (bad SQL, schema mismatch) should go straight to fault.
    /// </summary>
    private static bool IsTransientSqlError(SqlException ex) =>
        ex.Number is
            -2    or  // Timeout
             2    or  // Connection could not be made
            53    or  // Network path not found
          4060    or  // Cannot open database
         40197    or  // Service error processing request
         40501    or  // Service busy
         40613    or  // Database unavailable
         49918    or  // Not enough resources
         49919    or  // Cannot process create/update
         49920;       // Too many operations
}
