namespace ExamCorrection.Contracts.Authentication;

public record LoginRequest(
	string identifier,
	string Password,
	bool isEmail
);