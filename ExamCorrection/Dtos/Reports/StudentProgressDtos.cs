using System.Collections.Generic;
using ExamCorrection.Contracts.Analysis;

namespace ExamCorrection.Dtos.Reports;


public class StudentProgressDto
{
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public float OverallAverage { get; set; }
    public string PerformanceLevel { get; set; } = string.Empty;
    public List<StudentExamSummaryDto> ExamSummaries { get; set; } = [];
}

public class StudentExamSummaryDto
{
    public int ExamId { get; set; }
    public string ExamTitle { get; set; } = string.Empty;
    public float Score { get; set; }
    public float TotalScore { get; set; }
    public float Percentage { get; set; }
    public DateTime Date { get; set; }
    public List<GoalAnalysisDto> GoalAnalysis { get; set; } = [];
}
