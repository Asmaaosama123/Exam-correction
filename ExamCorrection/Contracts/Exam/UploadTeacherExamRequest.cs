using Microsoft.AspNetCore.Http;

namespace ExamCorrection.Contracts.TeacherExam;

public record UploadTeacherExamRequest(
    int ExamId,            // Id الامتحان الموجود مسبقًا
    IFormFile File,        // PDF أو صورة
    string QuestionsJson   // JSON كامل من process_teacher_sheet()
);
