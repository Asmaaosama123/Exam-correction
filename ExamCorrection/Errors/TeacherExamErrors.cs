public record TeacherExamErrors
{
    public static readonly Error NotFound =
        new("TeacherExam.NotFound", "لم يتم العثور على الامتحان", StatusCodes.Status400BadRequest);
}
