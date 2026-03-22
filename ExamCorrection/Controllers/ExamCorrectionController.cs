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

    [HttpGet("training-dataset/download")]
    [AllowAnonymous] // يسمح بالدخول بدون Token لكننا سنحميها بكلمة سر מخصص
    public IActionResult DownloadTrainingDatasetFilesZip([FromServices] IWebHostEnvironment webHostEnvironment, [FromQuery] string apiKey)
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
            return NotFound(new { message = "Dataset folder not found." });
        }

        var memoryStream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            var files = Directory.GetFiles(datasetFolder);
            foreach (var file in files)
            {
                var extension = Path.GetExtension(file).ToLower();
                if (extension == ".pdf" || extension == ".jpg" || extension == ".png" || extension == ".jpeg")
                {
                    // Adding file to zip archive manually to avoid Missing Method Exceptions if no FileSystem assembly is present.
                    var zipEntry = archive.CreateEntry(Path.GetFileName(file));
                    using (var entryStream = zipEntry.Open())
                    using (var fileStream = System.IO.File.OpenRead(file))
                    {
                        fileStream.CopyTo(entryStream);
                    }
                }
            }
        }

        memoryStream.Position = 0; // Reset stream position to the beginning
        return File(memoryStream, "application/zip", $"AI_Dataset_{DateTime.UtcNow:yyyyMMddHHmmss}.zip");
    }
   
}