namespace ExamCorrection.Errors;

public record ExamErrors
{
    public static readonly Error ExamNotFound =
        new("Exam.ClassNotFound", "لم يتم العثور على الامتحان", StatusCodes.Status400BadRequest);

    public static readonly Error DuplicatedExamName =
        new("Exam.DuplicatedExamName", "هذا الامتحان موجود بالفعل", StatusCodes.Status409Conflict);
}