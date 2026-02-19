using ExamCorrection.Abstractions;

namespace ExamCorrection.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ReportsController(IReportService reportService) : ControllerBase
{
    private readonly IReportService _reportService = reportService;

    [HttpGet("report-students-excel")]
    public async Task<IActionResult> ExportStudentsToExcel([FromQuery] int[] classIds)
    {
        var result = await _reportService.ExportStudentsToExcelAsync(classIds);

        if (!result.IsSuccess)
            return BadRequest(result.Error);

        var (fileContent, fileName) = result.Value;

        return File(fileContent, "application/octet-stream", fileName);
    }

    //[HttpGet("report-students-pdf")]
    //public async Task<IActionResult> ExportStudentsToPdf([FromQuery] List<int> classIds)
    //{
    //    var result = await _reportService.ExportStudentsToPdfAsync(classIds);

    //    if (!result.IsSuccess)
    //        return BadRequest(result.Error);

    //    var (fileContent, fileName) = result.Value;

    //    return File(fileContent, "application/octet-stream",fileName);
    //}

    [HttpGet("report-classes-excel")]
    public async Task<IActionResult> ExportClassesToExcel()
    {
        var result = await _reportService.ExportClassesToExcelAsync();

        if (!result.IsSuccess)
            return BadRequest(result.Error);

        var (fileContent, fileName) = result.Value;

        return File(fileContent, "application/octet-stream", fileName);
    }

    //[HttpGet("report-classes-pdf")]
    //public async Task<IActionResult> ExportClassesToPdf()
    //{
    //    var result = await _reportService.ExportClassesToPdfAsync();

    //    if (!result.IsSuccess)
    //        return BadRequest(result.Error);

    //    var (fileContent, fileName) = result.Value;

    //    return File(fileContent, "application/octet-stream", fileName);
    //}
}