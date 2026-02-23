using ExamCorrection.Contracts.Reports;

namespace ExamCorrection.Services;

public interface IReportService
{
    Task<Result<(byte[] FileContent, string FileName)>> ExportStudentsToExcelAsync(IEnumerable<int> classIds);
    Task<Result<(byte[] FileContent, string FileName)>> ExportStudentsToPdfAsync(IEnumerable<int> classIds);
    Task<Result<(byte[] FileContent, string FileName)>> ExportClassesToExcelAsync();
    Task<Result<(byte[] FileContent, string FileName)>> ExportClassesToPdfAsync();
    Task<Result<(byte[] FileContent, string FileName)>> ExportExamResultsToExcelAsync(int examId);
    Task<Result<(byte[] FileContent, string FileName)>> ExportExamResultsToPdfAsync(int examId);
    
}