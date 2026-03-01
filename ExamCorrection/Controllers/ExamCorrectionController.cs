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

    [HttpGet("training-dataset")]
    [AllowAnonymous] // يسمح بالدخول بدون Token لكننا سنحميها بكلمة سر מخصص
    public IActionResult GetTrainingDatasetFiles([FromServices] IWebHostEnvironment webHostEnvironment, [FromQuery] string apiKey)
    {
        // حماية بـ API Key بسيط خاص بفريق الـ AI
        const string SecretApiKey = "AI_DATASET_SECURE_KEY_2026";
        
        if (string.IsNullOrEmpty(apiKey) || apiKey != SecretApiKey)
        {
            return Unauthorized(new { message = "You are not authorized to access this dataset. Invalid API Key." });
        }

        var datasetFolder = Path.Combine(webHostEnvironment.WebRootPath, "AI-Dataset");
        
        if (!Directory.Exists(datasetFolder))
        {
            return Ok(new List<string>());
        }

        var files = Directory.GetFiles(datasetFolder)
                             .Select(f => $"/AI-Dataset/{Path.GetFileName(f)}")
                             .ToList();

        return Ok(files);
    }
   
}