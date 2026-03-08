namespace ExamCorrection.Contracts.Analysis;

public class QuestionAnalysisDto
{
    public int QuestionNumber { get; set; }

    public int CorrectCount { get; set; }

    public double SuccessRate { get; set; }
    
    public string QuestionDisplay { get; set; } = string.Empty;
}