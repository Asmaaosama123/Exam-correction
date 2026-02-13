namespace ExamCorrection.Entities;

public class Exam
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string PdfPath { get; set; } = string.Empty;
    public int NumberOfPages { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public double X { get; set; }
    public double Y { get; set; }
    public string OwnerId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }
    public ICollection<StudentExamPaper> StudentPapers { get; set; } = [];
}