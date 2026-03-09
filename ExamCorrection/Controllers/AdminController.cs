using ExamCorrection.Contracts.Admin;
using ExamCorrection.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExamCorrection.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class AdminController(IAdminService adminService) : ControllerBase
{
    private readonly IAdminService _adminService = adminService;
    private const string AdminEmail = "admin@exam-correction.com";

    private bool IsAdmin()
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        return email == AdminEmail;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();

        var result = await _adminService.GetStatsAsync(cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers(CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();

        var result = await _adminService.GetAllUsersAsync(cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();

        var result = await _adminService.CreateUserAsync(request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPut("users/{userId}")]
    public async Task<IActionResult> UpdateUser([FromRoute] string userId, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();

        var result = await _adminService.UpdateUserAsync(userId, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpDelete("users/{userId}")]
    public async Task<IActionResult> DeleteUser([FromRoute] string userId, CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();

        var result = await _adminService.DeleteUserAsync(userId, cancellationToken);
        return result.IsSuccess ? Ok() : result.ToProblem();
    }
}
