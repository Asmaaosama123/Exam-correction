namespace ExamCorrection.Contracts.Authentication;

public record AuthResponse(
	string Id,
	string FirstName,
	string LastName,
	string Token,
	int ExpiresIn,
	string RefreshToken,
	DateTime RefreshTokenExpiration
);