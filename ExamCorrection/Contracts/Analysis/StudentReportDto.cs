namespace ExamCorrection.Contracts.Analysis;

public class StudentReportDto
{
    public string StudentName { get; set; } = string.Empty;

    public int TotalCorrect { get; set; }

    public double Percentage { get; set; }

    public string Status { get; set; } = string.Empty; // ناجح / بحاجة دعم

    public List<GoalAnalysisDto> GoalAnalysis { get; set; } = new();

    public List<QuestionDetailDto> Answers { get; set; } = new();
}
