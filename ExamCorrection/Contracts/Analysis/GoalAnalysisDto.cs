namespace ExamCorrection.Contracts.Analysis;

public class GoalAnalysisDto
{
    public string GoalText { get; set; } = string.Empty;
    public double SuccessRate { get; set; }
    public List<int> QuestionNumbers { get; set; } = new();
}
