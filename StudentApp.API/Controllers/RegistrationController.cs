using Microsoft.AspNetCore.Mvc;
using StudentApp.API.Services;

namespace StudentApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RegistrationController : ControllerBase
{
    private readonly IRegistrationService _service;
    private readonly ILogger<RegistrationController> _logger;

    public RegistrationController(
        IRegistrationService service,
        ILogger<RegistrationController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    /// <summary>
    /// Submit a new student registration.
    /// Returns 202 Accepted immediately with a correlationId.
    /// Returns 503 if Worker is detected as down (fast fail).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(RegistrationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(RegistrationResult), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Register([FromBody] RegisterStudentRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _service.RegisterAsync(request);

        if (!result.Accepted)
        {
            // Worker is down — tell user immediately instead of letting them wait
            return StatusCode(StatusCodes.Status503ServiceUnavailable, result);
        }

        return Accepted(new { result.CorrelationId, result.Message });
    }

    /// <summary>
    /// Poll registration status by correlationId.
    /// Used as fallback when SignalR is not connected.
    /// </summary>
    [HttpGet("{correlationId}/status")]
    public async Task<IActionResult> GetStatus(string correlationId)
    {
        var status = await _service.GetStatusAsync(correlationId);
        return Ok(status);
    }

    /// <summary>
    /// System health — reads from Redis (Worker writes every 5s).
    /// Used by UI to show warning banners when services are degraded.
    /// </summary>
    [HttpGet("/api/system/health")]
    public async Task<IActionResult> GetSystemHealth()
    {
        var health = await _service.GetSystemHealthAsync();
        return Ok(health);
    }
}
