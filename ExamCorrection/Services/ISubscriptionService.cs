using ExamCorrection.Abstractions;
using ExamCorrection.Contracts.Subscriptions;
using ExamCorrection.Entities;

namespace ExamCorrection.Services;

public interface ISubscriptionService
{
    // Plans (Admin)
    Task<Result<IEnumerable<SubscriptionPlanDto>>> GetAllPlansAsync(CancellationToken cancellationToken = default);
    Task<Result<SubscriptionPlanDto>> CreatePlanAsync(CreateSubscriptionPlanRequest request, CancellationToken cancellationToken = default);
    Task<Result<SubscriptionPlanDto>> UpdatePlanAsync(int id, UpdateSubscriptionPlanRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeletePlanAsync(int id, CancellationToken cancellationToken = default);

    // Requests (User)
    Task<Result<SubscriptionRequestDto>> CreateRequestAsync(int planId, CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<SubscriptionRequestDto>>> GetMyRequestsAsync(CancellationToken cancellationToken = default);
    Task<Result<string>> InitiateOnlinePaymentAsync(int planId, CancellationToken cancellationToken = default);

    // Requests (Admin)
    Task<Result<IEnumerable<SubscriptionRequestDto>>> GetAllRequestsAsync(CancellationToken cancellationToken = default);
    Task<Result> ProcessRequestAsync(int requestId, ProcessSubscriptionRequest request, CancellationToken cancellationToken = default);
    Task ActivateSubscriptionInternalAsync(ApplicationUser user, SubscriptionPlan plan);
}
