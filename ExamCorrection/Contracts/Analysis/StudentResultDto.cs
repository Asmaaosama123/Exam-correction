namespace ExamCorrection.Contracts.Analysis;

public class StudentResultDto
{
    public string StudentName { get; set; } = string.Empty;

    public List<bool> Answers { get; set; } = new();

    public int TotalCorrect { get; set; }

    public double Percentage { get; set; }
}