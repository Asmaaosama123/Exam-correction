namespace ExamCorrection.Contracts.Admin;

public record UserDto(
    string Id,
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    bool IsDisabled,
    long MaxAllowedPages = 0,
    long UsedPages = 0,
    DateTime? SubscriptionExpiryUtc = null,
    bool IsSubscribed = false,
    int CorrectedPagesCount = 0
);
