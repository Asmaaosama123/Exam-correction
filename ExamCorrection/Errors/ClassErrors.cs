namespace ExamCorrection.Errors;

public record ClassErrors
{
    public static readonly Error ClassNotFound =
        new("Class.ClassNotFound", "لم يتم العثور على الفصل", StatusCodes.Status400BadRequest);

    public static readonly Error InvalidClassId =
        new("Class.InvalidClassId", "الرقم التسلسلي للفصل غير صالح", StatusCodes.Status400BadRequest);

    public static readonly Error DuplicatedClassName =
        new("Class.DuplicatedClassName", "اسم الفصل موجود بالفعل", StatusCodes.Status400BadRequest);
}