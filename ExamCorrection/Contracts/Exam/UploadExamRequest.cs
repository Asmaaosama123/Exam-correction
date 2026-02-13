namespace ExamCorrection.Contracts.Exam;

public record UploadExamRequest(
    IFormFile File,
    string Title,
    string Subject,
    double X,
    double Y
);