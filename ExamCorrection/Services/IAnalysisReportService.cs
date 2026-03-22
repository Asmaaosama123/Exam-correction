using ExamCorrection.Contracts.Reports;

namespace ExamCorrection.Services;

public interface IAnalysisReportService
{
    Task<Result<(byte[] FileContent, string FileName)>> ExportDetailedAnalysisToPdfAsync(DetailedAnalysisPdfRequestDto request);
    Task<Result<(byte[] FileContent, string FileName)>> ExportStudentProgressToPdfAsync(DetailedStudentProgressPdfRequestDto request, string userId);
}
