namespace ExamCorrection.Contracts.Exam;

public record FileExamResponse(
    byte[] File,
    string FileName,
    string ContentType
);