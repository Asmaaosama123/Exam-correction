namespace ExamCorrection.Contracts.Subscriptions;

public record SubscriptionRequestDto(
    int Id,
    string UserId,
    string UserFullName,
    int PlanId,
    string PlanName,
    string Status,
    DateTime RequestedAt,
    DateTime? ProcessedAt,
    string? AdminNotes
);

public record CreateSubscriptionRequest(
    int PlanId
);

public record ProcessSubscriptionRequest(
    string Status, // Approved, Rejected
    string? AdminNotes
);
