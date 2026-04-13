namespace ExamCorrection.Contracts.Admin;

public record UpdateUserRequest(
    string FirstName,
    string LastName,
    string? PhoneNumber,
    bool IsDisabled,
    long MaxAllowedPages = 0,
    DateTime? SubscriptionExpiryUtc = null,
    bool IsSubscribed = false
);
