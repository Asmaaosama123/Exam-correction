namespace ExamCorrection.Contracts.ExternalApi;

public record ExamCorrectionResponse(
    int StudentId,
    int ExamId,
    int Score,
    int Total,
    Object Details
);