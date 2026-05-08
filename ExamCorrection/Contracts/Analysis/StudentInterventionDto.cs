namespace ExamCorrection.Contracts.Analysis;

public class StudentInterventionDto
{
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public double Percentage { get; set; }
    public string ClassName { get; set; } = string.Empty;
}
