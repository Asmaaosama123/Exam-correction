namespace ExamCorrection.Contracts.Admin;

public record UpdateUserRequest(
    string FirstName,
    string LastName,
    string? PhoneNumber,
    bool IsDisabled,
    string? Email = null,
    string? Password = null,
    long MaxAllowedPages = 0,
    DateTime? SubscriptionExpiryUtc = null,
    bool IsSubscribed = false
);
