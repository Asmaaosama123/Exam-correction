namespace ExamCorrection.Contracts.Analysis;

public class ClassReportDto
{
    public int TotalStudents { get; set; }

    public double OverallPercentage { get; set; }

    public int PassedStudents { get; set; }

    public int FailedStudents { get; set; }

    public List<QuestionAnalysisDto> QuestionAnalysis { get; set; } = new();

    public List<GoalAnalysisDto> GoalAnalysis { get; set; } = new();
}
