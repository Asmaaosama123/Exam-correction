namespace ExamCorrection.Entities;

public class StudentExamPage
{
    public int Id { get; set; }
    public int StudentExamPaperId { get; set; }
    public int PageNumber { get; set; }
    public string BarcodeValue { get; set; } = string.Empty;
    public string BarcodeImagePath { get; set; } = string.Empty;

    public StudentExamPaper StudentExamPaper { get; set; } = default!;
}