namespace ExamCorrection.Contracts.AI;

public record StudentExamResult(
    int ExamId,
    int StudentId,
    int Score,
    int Total,
    string? AnnotatedImageUrl
);
