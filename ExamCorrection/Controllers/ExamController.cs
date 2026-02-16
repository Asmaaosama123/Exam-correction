using ExamCorrection.Contracts.TeacherExam;
using ExamCorrection.Services;

namespace ExamCorrection.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ExamController(IExamService examService,IExamAiService examAiService) : ControllerBase
{
    private readonly IExamService _examService = examService;
    private readonly IExamAiService _examAiService = examAiService;
    [HttpGet("")]
    public async Task<IActionResult> GetAll()
    {
        var result = await _examService.GetAllAsync();
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("{examId}")]
    public async Task<IActionResult> Get(int examId, CancellationToken cancellationToken)
    {
        var result = await _examService.GetAsync(examId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpDelete("{examId}")]
    public async Task<IActionResult> Delete(int examId, CancellationToken cancellationToken)
    {
        var result = await _examService.DeleteAsync(examId, cancellationToken);
        return result.IsSuccess ? NoContent() : result.ToProblem();
    }

    [HttpPost("upload-exam")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadExam([FromForm] UploadExamRequest request)
    {
        try 
        {
            var result = await _examService.UploadExamPdfAsync(request);
            return result.IsSuccess ? NoContent() : result.ToProblem();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UploadExam Exception] {ex}");
            return Problem(detail: ex.ToString(), title: "Upload Failed Exception");
        }
    }

    [HttpPost("generate-download-exams")]
    public async Task<IActionResult> GenerateAndDownload([FromBody] GenerateExamRequest request)
    {
        var result = await _examService.GenerateAndDownloadExamsAsync(request);

        return result.IsSuccess
            ? File(result.Value!.File, result.Value.ContentType, result.Value.FileName)
            : result.ToProblem();
    }
    [HttpPost("upload-teacher-exam")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadTeacherExam([FromForm] UploadTeacherExamRequest request)
    {
        var result = await _examService.UploadTeacherExamAsync(request);

        return result.IsSuccess
            ? Ok(result.Value)
            : Problem(detail: result.Error.Description, statusCode: result.Error.StatusCode);
    }
    [HttpPost("process")]
    public async Task<IActionResult> Process(IFormFile file)
    {
        var result = await _examAiService.ProcessExamAsync(file);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.Error);
    }

}