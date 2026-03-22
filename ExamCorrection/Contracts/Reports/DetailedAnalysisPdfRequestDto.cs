namespace ExamCorrection.Contracts.Reports;

public class StudentChartDto
{
    public int PaperId { get; set; }
    public string? GeneralRadarBase64 { get; set; }
    public string? StrengthRadarBase64 { get; set; }
    public string? WeaknessRadarBase64 { get; set; }
}

public class DetailedAnalysisPdfRequestDto
{
    public int ExamId { get; set; }
    public int? PaperId { get; set; }
    public string? RadarImageBase64 { get; set; }
    public string? BarChartImageBase64 { get; set; }
    public string? StrengthRadarImageBase64 { get; set; }
    public string? WeaknessRadarImageBase64 { get; set; }
    public string? ClassStrengthRadarImageBase64 { get; set; }
    public string? ClassWeaknessRadarImageBase64 { get; set; }
    public string? QuestionBarImageBase64 { get; set; }
    public List<StudentChartDto>? StudentCharts { get; set; }
}
