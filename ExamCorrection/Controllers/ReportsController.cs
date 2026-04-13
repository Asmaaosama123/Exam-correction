using ExamCorrection.Abstractions;

namespace ExamCorrection.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ReportsController(IReportService reportService) : ControllerBase
{
    private readonly IReportService _reportService = reportService;
    
    [HttpGet("ping")]
    [AllowAnonymous]
    public IActionResult Ping() => Ok("Reports Controller is reachable");

    [HttpGet("report-students-pdf")]
    public async Task<IActionResult> ExportStudentsToPdf([FromQuery] int[] classIds)
    {
        var result = await _reportService.ExportStudentsToPdfAsync(classIds);

        if (!result.IsSuccess)
            return BadRequest(result.Error);

        var (fileContent, fileName) = result.Value;

        return File(fileContent, "application/pdf", fileName);
    }

    [HttpGet("report-students-excel")]
    public async Task<IActionResult> ExportStudentsToExcel([FromQuery] int[] classIds)
    {
        var result = await _reportService.ExportStudentsToExcelAsync(classIds);

        if (!result.IsSuccess)
            return BadRequest(result.Error);

        var (fileContent, fileName) = result.Value;

        return File(fileContent, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("report-classes-pdf")]
    public async Task<IActionResult> ExportClassesToPdf()
    {
        var result = await _reportService.ExportClassesToPdfAsync();

        if (!result.IsSuccess)
            return BadRequest(result.Error);

        var (fileContent, fileName) = result.Value;

        return File(fileContent, "application/pdf", fileName);
    }

    [HttpGet("report-classes-excel")]
    public async Task<IActionResult> ExportClassesToExcel()
    {
        var result = await _reportService.ExportClassesToExcelAsync();

        if (!result.IsSuccess)
            return BadRequest(result.Error);

        var (fileContent, fileName) = result.Value;

        return File(fileContent, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("report-exam-results-excel")]
    public async Task<IActionResult> ExportExamResultsToExcel([FromQuery] int examId, [FromQuery] int? classId = null)
    {
        var result = await _reportService.ExportExamResultsToExcelAsync(examId, classId);

        if (!result.IsSuccess)
            return BadRequest(result.Error);

        var (fileContent, fileName) = result.Value;

        return File(fileContent, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("report-exam-results-pdf")]
    public async Task<IActionResult> ExportExamResultsToPdf([FromQuery] int examId, [FromQuery] int? classId = null)
    {
        var result = await _reportService.ExportExamResultsToPdfAsync(examId, classId);

        if (!result.IsSuccess)
            return BadRequest(result.Error);

        var (fileContent, fileName) = result.Value;

        return File(fileContent, "application/pdf", fileName);
    }

    [HttpGet("export-corrected-papers-pdf")]
    public async Task<IActionResult> ExportCorrectedPapersPdf([FromQuery] int examId, [FromQuery] string? teacherId = null, [FromQuery] int? classId = null)
    {
        var result = await _reportService.ExportCorrectedPapersPdfAsync(examId, teacherId, classId);

        if (!result.IsSuccess)
            return BadRequest(result.Error);

        var (fileContent, fileName) = result.Value;

        return File(fileContent, "application/pdf", fileName);
    }

    [HttpGet("export-corrected-papers-zip")]
    public async Task<IActionResult> ExportCorrectedPapersZip([FromQuery] int[] examIds, [FromQuery] string? teacherId = null)
    {
        var result = await _reportService.ExportCorrectedPapersZipAsync(examIds, teacherId);

        if (!result.IsSuccess)
            return BadRequest(result.Error);

        var (fileContent, fileName) = result.Value;

        return File(fileContent, "application/zip", fileName);
    }
}