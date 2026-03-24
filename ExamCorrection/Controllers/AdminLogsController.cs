using ExamCorrection.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExamCorrection.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminLogsController(ISystemLogService systemLogService) : ControllerBase
{
    [HttpGet("summary")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetErrorSummary()
    {
        var summary = await systemLogService.GetErrorSummaryAsync();
        return Ok(summary);
    }

    [HttpGet("details")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetErrorDetails([FromQuery] string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            return BadRequest("Error message is required.");

        var details = await systemLogService.GetErrorDetailsAsync(errorMessage);
        // We project to a safer DTO to avoid exposing the full User entity
        var result = details.Select(d => new
        {
            d.Id,
            d.ErrorMessage,
            d.ErrorDetails,
            d.ErrorSource,
            d.CreatedAt,
            d.IsResolved,
            d.OwnerId,
            UserFullName = d.User != null ? $"{d.User.FirstName} {d.User.LastName}".Trim() : "System/Unknown"
        });

        return Ok(result);
    }

    [HttpPut("resolve")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ResolveError([FromQuery] string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            return BadRequest("Error message is required.");

        var resolved = await systemLogService.ResolveErrorAsync(errorMessage);
        if (!resolved)
            return NotFound("No unresolved errors found with this message.");

        return Ok(new { Message = "Errors resolved successfully" });
    }

    [HttpPost("client-error")]
    [Authorize] // Allow any logged-in user (Teacher/Admin) to report frontend errors
    public async Task<IActionResult> LogClientError([FromBody] ClientErrorRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ErrorMessage))
            return BadRequest("Error message is required.");

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        await systemLogService.LogErrorAsync(
            request.ErrorMessage, 
            request.ErrorDetails ?? string.Empty, 
            request.ErrorSource ?? "FRONTEND", 
            userId);

        return Ok();
    }
}

public class ClientErrorRequest
{
    public string ErrorMessage { get; set; } = string.Empty;
    public string? ErrorDetails { get; set; }
    public string? ErrorSource { get; set; }
}
