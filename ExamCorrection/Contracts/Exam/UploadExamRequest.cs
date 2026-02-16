namespace ExamCorrection.Contracts.Exam;

public class UploadExamRequest
{
    public required IFormFile File { get; set; }
    public required string Title { get; set; }
    public required string Subject { get; set; }
    public string? BarcodeData { get; set; }
}