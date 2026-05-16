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
    long FreePagesCount = 0,
    long TotalCorrectedCount = 0,
    DateTime? SubscriptionExpiryUtc = null,
    bool IsSubscribed = false,
    int CorrectedPagesCount = 0,
    string? PlainPassword = null
);
