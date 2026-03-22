namespace ExamCorrection.Contracts.Admin;

public record CreateUserRequest(
    string FirstName,
    string LastName,
    string? Email,
    string? PhoneNumber,
    string Password
);
