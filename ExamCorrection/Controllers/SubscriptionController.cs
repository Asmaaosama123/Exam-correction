using ExamCorrection.Contracts.Subscriptions;
using ExamCorrection.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamCorrection.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class SubscriptionController(ISubscriptionService subscriptionService) : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService = subscriptionService;

    [HttpGet("plans")]
    public async Task<IActionResult> GetActivePlans(CancellationToken cancellationToken)
    {
        var result = await _subscriptionService.GetAllPlansAsync(cancellationToken);
        if (result.IsSuccess)
        {
            // Only show active plans to teachers
            return Ok(result.Value.Where(p => p.IsActive));
        }
        return result.ToProblem();
    }

    [HttpPost("request/{planId}")]
    public async Task<IActionResult> RequestSubscription([FromRoute] int planId, CancellationToken cancellationToken)
    {
        var result = await _subscriptionService.CreateRequestAsync(planId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("my-requests")]
    public async Task<IActionResult> GetMyRequests(CancellationToken cancellationToken)
    {
        var result = await _subscriptionService.GetMyRequestsAsync(cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost("initiate-payment/{planId}")]
    public async Task<IActionResult> InitiatePayment([FromRoute] int planId, CancellationToken cancellationToken)
    {
        var result = await _subscriptionService.InitiateOnlinePaymentAsync(planId, cancellationToken);
        return result.IsSuccess ? Ok(new { paymentUrl = result.Value }) : result.ToProblem();
    }
}
