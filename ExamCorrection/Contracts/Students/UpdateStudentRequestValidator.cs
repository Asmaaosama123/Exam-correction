namespace ExamCorrection.Contracts.Students;

public class UpdateStudentRequestValidator : AbstractValidator<UpdateStudentRequest>
{
    public UpdateStudentRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty()
            .Length(3, 150);

        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage(GlobalErrors.InValideEmail);

        RuleFor(x => x.MobileNumber)
            .Matches(RegexPatterns.MobileNumber)
            .When(x => !string.IsNullOrWhiteSpace(x.MobileNumber))
            .WithMessage(GlobalErrors.InValideMobileNumber);
    }
}