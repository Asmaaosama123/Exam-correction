namespace ExamCorrection.Errors;

public record ExamErrors
{
    public static readonly Error ExamNotFound =
        new("Exam.ClassNotFound", "لم يتم العثور على الامتحان", StatusCodes.Status400BadRequest);

    public static readonly Error DuplicatedExamName =
        new("Exam.DuplicatedExamName", "هذا الامتحان موجود بالفعل", StatusCodes.Status409Conflict);

    public static readonly Error PdfFileNotFound =
        new("Exam.PdfFileNotFound", "ملف الـ PDF الخاص بالامتحان غير موجود على السيرفر", StatusCodes.Status404NotFound);

    public static readonly Error FontNotFound =
        new("Exam.FontNotFound", "ملف الخط المطلوب غير موجود على السيرفر", StatusCodes.Status500InternalServerError);
}