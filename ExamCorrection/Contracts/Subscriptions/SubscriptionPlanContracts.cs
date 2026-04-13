namespace ExamCorrection.Contracts.Subscriptions;

public record SubscriptionPlanDto(
    int Id,
    string Name,
    string? Description,
    decimal Price,
    long MaxAllowedPages,
    int DurationValue,
    string DurationUnit,
    bool IsActive
);

public record CreateSubscriptionPlanRequest(
    string Name,
    string? Description,
    decimal Price,
    long MaxAllowedPages,
    int DurationValue,
    string DurationUnit
);

public record UpdateSubscriptionPlanRequest(
    string Name,
    string? Description,
    decimal Price,
    long MaxAllowedPages,
    int DurationValue,
    string DurationUnit,
    bool IsActive
);
