namespace ExamCorrection.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ExamCorrectionController(IExamService examService) : ControllerBase
{
    private readonly IExamService _examService = examService;

    [HttpPost("upload-exam-answers")]
    public async Task<IActionResult> UploadExamAnswers(IFormFile file, CancellationToken cancellationToken)
    {
        var result = await _examService.UploadAndSaveExamAnswersAsync(file, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
   
}