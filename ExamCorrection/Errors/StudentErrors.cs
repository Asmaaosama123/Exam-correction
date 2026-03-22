namespace ExamCorrection.Errors;

public record StudentErrors
{
    public static readonly Error StudentNotFound =
        new("Student.StudentNotFound", "لم يتم العثور على الطالب", StatusCodes.Status404NotFound);

    public static readonly Error NoStudentsFound =
        new("Student.NoStudentsFound", "هذا الفصل لا يحتوي علي طلاب", StatusCodes.Status404NotFound);

    public static readonly Error InvalidStudentId =
        new("Student.InvalidStudentId", "الرقم التسلسلي للطالب غير صالح", StatusCodes.Status400BadRequest);

    public static readonly Error EmptyFile =
        new("File.Empty", "الملف الذي تم تحميله فارغ أو مفقود", StatusCodes.Status400BadRequest);

    public static readonly Error DuplicatedMobileNumber =
        new("File.DuplicatedMobileNumber", "رقم الهاتف موجود بالفعل", StatusCodes.Status400BadRequest);

    public static readonly Error DuplicatedEmail =
        new("File.DuplicatedEmail", "البريد الإلكتروني موجود بالفعل", StatusCodes.Status400BadRequest);
}