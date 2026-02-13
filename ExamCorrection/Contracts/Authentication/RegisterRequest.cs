namespace ExamCorrection.Contracts.Authentication;

public record RegisterRequest(
    string? PhoneNumber,
    string? Email,
    string Password,
	string FirstName,
	string LastName,
    bool IsEmail
);