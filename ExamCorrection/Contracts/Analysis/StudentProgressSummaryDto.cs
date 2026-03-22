namespace ExamCorrection.Contracts.Analysis;

public class StudentProgressSummaryDto
{
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public double OverallAverage { get; set; }
    public string PerformanceLevel { get; set; } = string.Empty;
    public int ExamsTaken { get; set; }
    public List<string> Strengths { get; set; } = new();
    public List<string> Weaknesses { get; set; } = new();
    public double Change { get; set; }
}
