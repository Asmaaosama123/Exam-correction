namespace ExamCorrection.Services;

using ExamCorrection.Contracts.AI;
using Microsoft.AspNetCore.Http;

public interface IExamAiService
{
    Task<Result<McqResponse>> ProcessExamAsync(IFormFile pdfFile);
}
