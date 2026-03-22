namespace ExamCorrection.Errors;

public record FileErrors
{
    public static readonly Error EmptyFile =
        new("File.Empty", "الملف الذي تم تحميله فارغ أو مفقود", StatusCodes.Status400BadRequest);

    public static readonly Error NotAllowedExtension =
        new("File.NotAllowedExtension", "يُسمح فقط بالملفات .csv و.xlsx", StatusCodes.Status400BadRequest);

    public static readonly Error MaxFileSize =
        new("File.MaxFileSize", "لا يمكن أن يكون حجم الملف أكثر من 7 ميجا بايت", StatusCodes.Status400BadRequest);

    public static readonly Error OnlyPdfAllowed =
        new("File.OnlyPdfAllowed", "مسموح فقط بملفات .pdf!", StatusCodes.Status400BadRequest);

    public static readonly Error FileNotFound =
        new("File.FileNotFound", "هذا الامتحان غير موجود", StatusCodes.Status400BadRequest);
}