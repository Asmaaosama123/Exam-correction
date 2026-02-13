namespace ExamCorrection.Contracts.Exam;

public record ExamResponse(
    int Id,
    string Title, 
    string Subject,
    string PdfPath,
    int NumberOfPages,
    DateTime CreatedAt
);