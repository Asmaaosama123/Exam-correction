using ExamCorrection.Abstractions;
using ExamCorrection.Clients.Tap;
using ExamCorrection.Contracts.Subscriptions;
using ExamCorrection.Entities;
using ExamCorrection.Persistance;
using ExamCorrection.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExamCorrection.Services;

public class SubscriptionService(
    ApplicationDbContext dbContext,
    IUserContext userContext,
    UserManager<ApplicationUser> userManager,
    ITapService tapService,
    IOptions<TapSettings> tapSettings) : ISubscriptionService
{
    private readonly ApplicationDbContext _dbContext = dbContext;
    private readonly IUserContext _userContext = userContext;
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly ITapService _tapService = tapService;
    private readonly TapSettings _tapSettings = tapSettings.Value;

    #region Plans (Admin)

    public async Task<Result<IEnumerable<SubscriptionPlanDto>>> GetAllPlansAsync(CancellationToken cancellationToken = default)
    {
        var plans = await _dbContext.SubscriptionPlans
            .OrderBy(p => p.Price)
            .Select(p => new SubscriptionPlanDto(
                p.Id,
                p.Name,
                p.Description,
                p.Price,
                p.MaxAllowedPages,
                p.DurationValue,
                p.DurationUnit,
                p.IsActive
            ))
            .ToListAsync(cancellationToken);

        return Result.Success<IEnumerable<SubscriptionPlanDto>>(plans);
    }

    public async Task<Result<SubscriptionPlanDto>> CreatePlanAsync(CreateSubscriptionPlanRequest request, CancellationToken cancellationToken = default)
    {
        var plan = new SubscriptionPlan
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            MaxAllowedPages = request.MaxAllowedPages,
            DurationValue = request.DurationValue,
            DurationUnit = request.DurationUnit,
            IsActive = true
        };

        _dbContext.SubscriptionPlans.Add(plan);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new SubscriptionPlanDto(
            plan.Id, plan.Name, plan.Description, plan.Price, plan.MaxAllowedPages, plan.DurationValue, plan.DurationUnit, plan.IsActive));
    }

    public async Task<Result<SubscriptionPlanDto>> UpdatePlanAsync(int id, UpdateSubscriptionPlanRequest request, CancellationToken cancellationToken = default)
    {
        var plan = await _dbContext.SubscriptionPlans.FindAsync(id);
        if (plan == null) return Result.Failure<SubscriptionPlanDto>(new Error("Plans.NotFound", "Plan not found", StatusCodes.Status404NotFound));

        plan.Name = request.Name;
        plan.Description = request.Description;
        plan.Price = request.Price;
        plan.MaxAllowedPages = request.MaxAllowedPages;
        plan.DurationValue = request.DurationValue;
        plan.DurationUnit = request.DurationUnit;
        plan.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new SubscriptionPlanDto(
            plan.Id, plan.Name, plan.Description, plan.Price, plan.MaxAllowedPages, plan.DurationValue, plan.DurationUnit, plan.IsActive));
    }

    public async Task<Result> DeletePlanAsync(int id, CancellationToken cancellationToken = default)
    {
        var plan = await _dbContext.SubscriptionPlans.FindAsync(id);
        if (plan == null) return Result.Failure(new Error("Plans.NotFound", "Plan not found", StatusCodes.Status404NotFound));

        _dbContext.SubscriptionPlans.Remove(plan);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    #endregion

    #region Requests (User)

    public async Task<Result<SubscriptionRequestDto>> CreateRequestAsync(int planId, CancellationToken cancellationToken = default)
    {
        var plan = await _dbContext.SubscriptionPlans.FindAsync(planId);
        if (plan == null || !plan.IsActive) 
            return Result.Failure<SubscriptionRequestDto>(new Error("Plans.Invalid", "Selected plan is unavailable", StatusCodes.Status400BadRequest));

        var existingRequest = await _dbContext.SubscriptionRequests
            .FirstOrDefaultAsync(r => r.UserId == _userContext.UserId && r.Status == "Pending", cancellationToken);
        
        if (existingRequest != null)
            return Result.Failure<SubscriptionRequestDto>(new Error("Requests.AlreadyExists", "You already have a pending request", StatusCodes.Status400BadRequest));

        var subscriptionRequest = new SubscriptionRequest
        {
            UserId = _userContext.UserId!,
            PlanId = planId,
            Status = "Pending",
            RequestedAt = DateTime.UtcNow
        };

        _dbContext.SubscriptionRequests.Add(subscriptionRequest);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new SubscriptionRequestDto(
            subscriptionRequest.Id,
            subscriptionRequest.UserId,
            "Me", // Name will be fetched in list
            planId,
            plan.Name,
            subscriptionRequest.Status,
            subscriptionRequest.RequestedAt,
            null,
            null
        ));
    }

    public async Task<Result<IEnumerable<SubscriptionRequestDto>>> GetMyRequestsAsync(CancellationToken cancellationToken = default)
    {
        var requests = await _dbContext.SubscriptionRequests
            .Where(r => r.UserId == _userContext.UserId)
            .OrderByDescending(r => r.RequestedAt)
            .Select(r => new SubscriptionRequestDto(
                r.Id,
                r.UserId,
                "", // User Context current user name or handled in UI
                r.PlanId,
                r.Plan!.Name,
                r.Status,
                r.RequestedAt,
                r.ProcessedAt,
                r.AdminNotes
            ))
            .ToListAsync(cancellationToken);

        return Result.Success<IEnumerable<SubscriptionRequestDto>>(requests);
    }

    public async Task<Result<string>> InitiateOnlinePaymentAsync(int planId, CancellationToken cancellationToken = default)
    {
        var plan = await _dbContext.SubscriptionPlans.FindAsync(planId);
        if (plan == null || !plan.IsActive)
            return Result.Failure<string>(new Error("Plans.Invalid", "Selected plan is unavailable", StatusCodes.Status400BadRequest));

        var user = await _userManager.FindByIdAsync(_userContext.UserId!);
        if (user == null)
            return Result.Failure<string>(new Error("Users.NotFound", "User not found", StatusCodes.Status404NotFound));

        // Create the pending request
        var subscriptionRequest = new SubscriptionRequest
        {
            UserId = user.Id,
            PlanId = planId,
            Status = "PaymentPending",
            RequestedAt = DateTime.UtcNow
        };

        _dbContext.SubscriptionRequests.Add(subscriptionRequest);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Call Tap API
        var chargeRequest = new CreateChargeRequest
        {
            Amount = plan.Price,
            Currency = "SAR", // Or from config
            Description = $"Subscription to {plan.Name} plan",
            Customer = new TapCustomer
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email ?? ""
            },
            Redirect = new TapRedirect { Url = _tapSettings.ReturnUrl },
            Post = new TapPost { Url = _tapSettings.WebhookUrl },
            Metadata = new Dictionary<string, string>
            {
                { "RequestId", subscriptionRequest.Id.ToString() },
                { "PlanId", planId.ToString() }
            }
        };

        var tapResult = await _tapService.CreateChargeAsync(chargeRequest, cancellationToken);
        if (tapResult.IsFailure)
        {
            subscriptionRequest.Status = "Failed";
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Failure<string>(tapResult.Error);
        }

        // Save Charge ID
        subscriptionRequest.TapChargeId = tapResult.Value.Id;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(tapResult.Value.Transaction.Url);
    }

    #endregion

    #region Requests (Admin)

    public async Task<Result<IEnumerable<SubscriptionRequestDto>>> GetAllRequestsAsync(CancellationToken cancellationToken = default)
    {
        var requests = await _dbContext.SubscriptionRequests
            .Include(r => r.User)
            .Include(r => r.Plan)
            .OrderByDescending(r => r.RequestedAt)
            .Select(r => new SubscriptionRequestDto(
                r.Id,
                r.UserId,
                (r.User!.FirstName + " " + r.User.LastName).Trim(),
                r.PlanId,
                r.Plan!.Name,
                r.Status,
                r.RequestedAt,
                r.ProcessedAt,
                r.AdminNotes
            ))
            .ToListAsync(cancellationToken);

        return Result.Success<IEnumerable<SubscriptionRequestDto>>(requests);
    }

    public async Task<Result> ProcessRequestAsync(int requestId, ProcessSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        var subscriptionRequest = await _dbContext.SubscriptionRequests
            .Include(r => r.Plan)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (subscriptionRequest == null)
            return Result.Failure(new Error("Requests.NotFound", "Request not found", StatusCodes.Status404NotFound));

        if (subscriptionRequest.Status != "Pending")
            return Result.Failure(new Error("Requests.Processed", "Request already processed", StatusCodes.Status400BadRequest));

        subscriptionRequest.Status = request.Status;
        subscriptionRequest.AdminNotes = request.AdminNotes;
        subscriptionRequest.ProcessedAt = DateTime.UtcNow;

        if (request.Status == "Approved")
        {
            await ActivateSubscriptionInternalAsync(subscriptionRequest.User!, subscriptionRequest.Plan!);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task ActivateSubscriptionInternalAsync(ApplicationUser user, SubscriptionPlan plan)
    {
        // Calculate Expiry
        DateTime expiry = DateTime.UtcNow;
        if (plan.DurationUnit == "Month") expiry = expiry.AddMonths(plan.DurationValue);
        else if (plan.DurationUnit == "Term") expiry = expiry.AddMonths(plan.DurationValue * 4); // Term = 4 months
        else if (plan.DurationUnit == "Year") expiry = expiry.AddYears(plan.DurationValue);

        user.IsSubscribed = true;
        user.MaxAllowedPages += plan.MaxAllowedPages;

        // If already active, extend. If not, start from now.
        var baseDate = (user.SubscriptionExpiryUtc != null && user.SubscriptionExpiryUtc > DateTime.UtcNow)
            ? user.SubscriptionExpiryUtc.Value
            : DateTime.UtcNow;

        if (plan.DurationUnit == "Month") user.SubscriptionExpiryUtc = baseDate.AddMonths(plan.DurationValue);
        else if (plan.DurationUnit == "Term") user.SubscriptionExpiryUtc = baseDate.AddMonths(plan.DurationValue * 4);
        else if (plan.DurationUnit == "Year") user.SubscriptionExpiryUtc = baseDate.AddYears(plan.DurationValue);
    }

    #endregion
}
