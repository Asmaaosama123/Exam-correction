namespace ExamCorrection.Contracts.Exam;

public record GenerateExamRequest(
    int ExamId,
    int ClassId
);