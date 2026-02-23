namespace ExamCorrection.Contracts.Reports;

public class ExamResultsExportViewModel
{
    public string ExamTitle { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public List<ExamResultItemViewModel> Results { get; set; } = new();
}

public class ExamResultItemViewModel
{
    public string StudentName { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public double? Score { get; set; }
    public int? TotalQuestions { get; set; }
    public DateTime GeneratedAt { get; set; }
}
