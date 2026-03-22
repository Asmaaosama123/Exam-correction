namespace ExamCorrection.Contracts.Authentication;

public record ConfirmEmailRequest(
	string UserId,
	string Code
);