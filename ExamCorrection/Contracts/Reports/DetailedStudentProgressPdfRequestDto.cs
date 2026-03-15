namespace ExamCorrection.Contracts.Reports;

public class DetailedStudentProgressPdfRequestDto
{
    public int? StudentId { get; set; }
    public string? ProgressChartBase64 { get; set; }
    public string? OverviewChartBase64 { get; set; }
}
