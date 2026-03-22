using ExamCorrection.Abstractions;
using ExamCorrection.Contracts.Reports;
using ExamCorrection.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamCorrection.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class AnalysisReportsController(IAnalysisReportService analysisReportService) : ControllerBase
{
    private readonly IAnalysisReportService _analysisReportService = analysisReportService;

    [HttpPost("report-detailed-analysis-pdf")]
    public async Task<IActionResult> ExportDetailedAnalysisToPdf([FromBody] DetailedAnalysisPdfRequestDto request)
    {
        try 
        {
            var result = await _analysisReportService.ExportDetailedAnalysisToPdfAsync(request);

            if (!result.IsSuccess)
            {
                System.IO.File.WriteAllText("error_log.txt", "Bad Request: " + System.Text.Json.JsonSerializer.Serialize(result.Error));
                return BadRequest(result.Error);
            }

            var (fileContent, fileName) = result.Value;

            return File(fileContent, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            System.IO.File.WriteAllText("error_log_ex.txt", ex.ToString());
            throw;
        }
    }

    [HttpPost("report-student-progress-pdf")]
    public async Task<IActionResult> ExportStudentProgressToPdf([FromBody] DetailedStudentProgressPdfRequestDto request)
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var result = await _analysisReportService.ExportStudentProgressToPdfAsync(request, userId);

            if (!result.IsSuccess)
                return BadRequest(result.Error);

            var (fileContent, fileName) = result.Value;

            return File(fileContent, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            System.IO.File.WriteAllText("error_log_progress_ex.txt", ex.ToString());
            throw;
        }
    }
}
