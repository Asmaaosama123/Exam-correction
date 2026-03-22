namespace ExamCorrection.Entities;

public class ExamPage
{
    public int Id { get; set; }
    public int ExamId { get; set; }
    public int PageNumber { get; set; }
    public double X { get; set; }
    public double Y { get; set; }

    public Exam Exam { get; set; } = default!;
}