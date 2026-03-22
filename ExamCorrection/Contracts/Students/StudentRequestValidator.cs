namespace ExamCorrection.Contracts.Students;

public class StudentRequestValidator : AbstractValidator<StudentRequest>
{
    public StudentRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty()
            .Length(3, 150);

        RuleFor(x => x.MobileNumber)
            .Matches(RegexPatterns.MobileNumber)
            .When(x => !string.IsNullOrWhiteSpace(x.MobileNumber))
            .WithMessage(GlobalErrors.InValideMobileNumber);
    }
}