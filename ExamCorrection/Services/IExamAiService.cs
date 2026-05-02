namespace ExamCorrection.Services;

using ExamCorrection.Contracts.AI;
using Microsoft.AspNetCore.Http;

public interface IExamAiService
{
    Task<Result<ExamResultsDto>> ProcessExamAsync(IFormFile file, int? examId = null);
    Task<Result<JsonDocument>> AnalyzeTemplateAsync(IFormFile file);
}
