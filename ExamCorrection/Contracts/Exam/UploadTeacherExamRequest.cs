using Microsoft.AspNetCore.Http;

namespace ExamCorrection.Contracts.TeacherExam;

public record UploadTeacherExamRequest(
    int? ExamId,           // Id الامتحان الموجود مسبقًا (اختياري، إن وجد)
    string? Title,         // اسم الامتحان (للامتحانات بدون باركود)
    string? Subject,       // المادة الدراسية (للامتحانات بدون باركود)
    bool? IsBarcode,       // هل يدعم الباركود؟ (للامتحانات بدون باركود)
    IFormFile File,        // PDF أو صورة
    string QuestionsJson,  // JSON كامل من process_teacher_sheet()
    int? PageCount = null  // عدد صفحات الامتحان (اختياري)
);
