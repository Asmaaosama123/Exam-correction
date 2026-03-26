using Microsoft.AspNetCore.Http;

namespace ExamCorrection.Contracts.Admin;

public record TeacherExamSummaryDto(
    int ExamId,
    string Title,
    string Subject,
    int PaperCount,
    DateTime LastCorrectedAt
);
