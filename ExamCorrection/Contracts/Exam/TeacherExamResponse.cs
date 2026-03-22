namespace ExamCorrection.Contracts.TeacherExam;

public record TeacherExamResponse(
    int ExamId,             // Id الامتحان
    string PdfPath,          // مسار الملف المحفوظ
    string QuestionsJson     // JSON كامل بالأسئلة والـ ROIs
);
