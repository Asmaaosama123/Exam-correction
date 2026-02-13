namespace ExamCorrection.Contracts.Class;

public class ClassRequestValidator : AbstractValidator<ClassRequest>
{
    public ClassRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);
    }
}