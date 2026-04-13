using ExamCorrection.Contracts.Admin;
using ExamCorrection.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExamCorrection.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")]
public class AdminController(IAdminService adminService) : ControllerBase
{
    private readonly IAdminService _adminService = adminService;
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var result = await _adminService.GetStatsAsync(cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("advanced-stats")]
    public async Task<IActionResult> GetAdvancedStats(CancellationToken cancellationToken)
    {
        var result = await _adminService.GetAdvancedStatsAsync(cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers(CancellationToken cancellationToken)
    {
        var result = await _adminService.GetAllUsersAsync(cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await _adminService.CreateUserAsync(request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPut("users/{userId}")]
    public async Task<IActionResult> UpdateUser([FromRoute] string userId, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await _adminService.UpdateUserAsync(userId, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpDelete("users/{userId}")]
    public async Task<IActionResult> DeleteUser([FromRoute] string userId, CancellationToken cancellationToken)
    {
        var result = await _adminService.DeleteUserAsync(userId, cancellationToken);
        return result.IsSuccess ? Ok() : result.ToProblem();
    }

    [HttpGet("users/{userId}/exams")]
    public async Task<IActionResult> GetTeacherExams([FromRoute] string userId, CancellationToken cancellationToken)
    {
        var result = await _adminService.GetTeacherExamsAsync(userId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
    {
        var result = await _adminService.GetSettingsAsync(cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost("settings/{key}")]
    public async Task<IActionResult> UpdateSetting([FromRoute] string key, [FromBody] string value, CancellationToken cancellationToken)
    {
        var result = await _adminService.UpdateSettingAsync(key, value, cancellationToken);
        return result.IsSuccess ? Ok() : result.ToProblem();
    }

    #region Subscription Plans

    [HttpGet("plans")]
    public async Task<IActionResult> GetAllPlans([FromServices] ISubscriptionService subscriptionService, CancellationToken cancellationToken)
    {
        var result = await subscriptionService.GetAllPlansAsync(cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost("plans")]
    public async Task<IActionResult> CreatePlan([FromServices] ISubscriptionService subscriptionService, [FromBody] ExamCorrection.Contracts.Subscriptions.CreateSubscriptionPlanRequest request, CancellationToken cancellationToken)
    {
        var result = await subscriptionService.CreatePlanAsync(request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPut("plans/{id}")]
    public async Task<IActionResult> UpdatePlan([FromServices] ISubscriptionService subscriptionService, [FromRoute] int id, [FromBody] ExamCorrection.Contracts.Subscriptions.UpdateSubscriptionPlanRequest request, CancellationToken cancellationToken)
    {
        var result = await subscriptionService.UpdatePlanAsync(id, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpDelete("plans/{id}")]
    public async Task<IActionResult> DeletePlan([FromServices] ISubscriptionService subscriptionService, [FromRoute] int id, CancellationToken cancellationToken)
    {
        var result = await subscriptionService.DeletePlanAsync(id, cancellationToken);
        return result.IsSuccess ? Ok() : result.ToProblem();
    }

    #endregion

    #region Subscription Requests

    [HttpGet("subscription-requests")]
    public async Task<IActionResult> GetAllSubscriptionRequests([FromServices] ISubscriptionService subscriptionService, CancellationToken cancellationToken)
    {
        var result = await subscriptionService.GetAllRequestsAsync(cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost("subscription-requests/{id}/process")]
    public async Task<IActionResult> ProcessSubscriptionRequest([FromServices] ISubscriptionService subscriptionService, [FromRoute] int id, [FromBody] ExamCorrection.Contracts.Subscriptions.ProcessSubscriptionRequest request, CancellationToken cancellationToken)
    {
        var result = await subscriptionService.ProcessRequestAsync(id, request, cancellationToken);
        return result.IsSuccess ? Ok() : result.ToProblem();
    }

    #endregion
}
