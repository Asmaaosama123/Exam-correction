namespace ExamCorrection.Errors;

public record AiErrors
{
    public static readonly Error ScanFailed =
        new("AI.ScanFailed", "فشل مسح الباركود", StatusCodes.Status400BadRequest);

    public static readonly Error NoBarcodesFound =
        new("AI.NoBarcodesFound", "لم يتم العثور على أي باركود", StatusCodes.Status400BadRequest);

    public static readonly Error ExamNotFoundInDb =
        new("AI.ExamNotFoundInDb", "لم يتم العثور على الامتحان في قاعدة البيانات", StatusCodes.Status400BadRequest);

    public static readonly Error McqProcessingFailed =
        new("AI.McqProcessingFailed", "فشل معالجة الأسئلة", StatusCodes.Status400BadRequest);

    
    public static readonly Error McqFailed = new Error("AI.McqFailed", "فشل في تحليل MCQ", StatusCodes.Status400BadRequest);
    public static readonly Error NoFilesProvided = new Error("AI.NoFilesProvided", "لم يتم رفع أي ملف", StatusCodes.Status400BadRequest);



}
