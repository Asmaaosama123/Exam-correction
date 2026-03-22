namespace ExamCorrection.Contracts.Authentication;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
	public LoginRequestValidator()
	{
        RuleFor(x => x.Password).NotEmpty();
	}
}