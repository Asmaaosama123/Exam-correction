namespace ExamCorrection.Contracts.Admin;

public record UpdateUserRequest(
    string FirstName,
    string LastName,
    string? PhoneNumber,
    bool IsDisabled
);
