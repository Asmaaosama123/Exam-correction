using ExamCorrection.Clients.Tap;
using ExamCorrection.Entities;
using ExamCorrection.Persistance;
using ExamCorrection.Services;
using ExamCorrection.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExamCorrection.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentController(
    ApplicationDbContext dbContext,
    ISubscriptionService subscriptionService,
    ITapService tapService,
    IOptions<TapSettings> tapSettings) : ControllerBase
{
    private readonly ApplicationDbContext _dbContext = dbContext;
    private readonly ISubscriptionService _subscriptionService = subscriptionService;
    private readonly ITapService _tapService = tapService;
    private readonly TapSettings _tapSettings = tapSettings.Value;

    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string tap_id)
    {
        // When user is redirected back to us, we can check the status and redirect to frontend
        return Redirect($"{_tapSettings.FrontendUrl}/payment-success?charge_id={tap_id}");
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromBody] ChargeResponse charge)
    {
        if (charge.Status == "CAPTURED")
        {
            // Find the request
            var subscriptionRequest = await _dbContext.SubscriptionRequests
                .Include(r => r.User)
                .Include(r => r.Plan)
                .FirstOrDefaultAsync(r => r.TapChargeId == charge.Id);

            if (subscriptionRequest != null && subscriptionRequest.Status == "PaymentPending")
            {
                subscriptionRequest.Status = "Approved";
                subscriptionRequest.PaymentStatus = "Captured";
                subscriptionRequest.ProcessedAt = DateTime.UtcNow;

                // Call the activation logic
                await _subscriptionService.ActivateSubscriptionInternalAsync(subscriptionRequest.User!, subscriptionRequest.Plan!);
                
                await _dbContext.SaveChangesAsync();
            }
        }
        else if (charge.Status == "FAILED" || charge.Status == "VOIDED" || charge.Status == "CANCELLED")
        {
             var subscriptionRequest = await _dbContext.SubscriptionRequests
                .FirstOrDefaultAsync(r => r.TapChargeId == charge.Id);
             
             if(subscriptionRequest != null)
             {
                 subscriptionRequest.Status = "Rejected";
                 subscriptionRequest.PaymentStatus = charge.Status;
                 subscriptionRequest.ProcessedAt = DateTime.UtcNow;
                 await _dbContext.SaveChangesAsync();
             }
        }

        return Ok();
    }
}
