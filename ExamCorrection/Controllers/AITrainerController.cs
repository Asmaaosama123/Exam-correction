using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ExamCorrection.Contracts.AITrainer;
using System.IO.Compression;

namespace ExamCorrection.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "AITrainer")]
public class AITrainerController(IWebHostEnvironment webHostEnvironment) : ControllerBase
{
    private readonly IWebHostEnvironment _webHostEnvironment = webHostEnvironment;

    [HttpGet("dataset-files")]
    public IActionResult GetDatasetFiles()
    {
        var datasetFolder = Path.Combine(_webHostEnvironment.WebRootPath, "AI-Dataset");
        
        if (!Directory.Exists(datasetFolder))
        {
            return Ok(new List<DatasetFileDto>());
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/api/AITrainer/file/";
        var files = Directory.GetFiles(datasetFolder);
        
        var fileList = new List<DatasetFileDto>();

        foreach (var file in files)
        {
            var extension = Path.GetExtension(file).ToLower();
            if (extension == ".pdf" || extension == ".jpg" || extension == ".png" || extension == ".jpeg")
            {
                var fileInfo = new FileInfo(file);
                fileList.Add(new DatasetFileDto
                {
                    FileName = fileInfo.Name,
                    FileUrl = baseUrl + Uri.EscapeDataString(fileInfo.Name),
                    CreationTime = fileInfo.CreationTime
                });
            }
        }

        return Ok(fileList.OrderByDescending(f => f.CreationTime).ToList());
    }

    [HttpPost("dataset-download")]
    public IActionResult DownloadSelectedDatasetFiles([FromBody] DownloadDatasetRequest request)
    {
        if (request?.SelectedFiles == null || !request.SelectedFiles.Any())
        {
            return BadRequest(new { message = "No files selected." });
        }

        var datasetFolder = Path.Combine(_webHostEnvironment.WebRootPath, "AI-Dataset");
        
        if (!Directory.Exists(datasetFolder))
        {
            return NotFound(new { message = "Dataset folder not found." });
        }

        var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            foreach (var fileName in request.SelectedFiles)
            {
                // Validate file name to prevent directory traversal attacks
                if (fileName.Contains("..") || fileName.Contains("/") || fileName.Contains("\\"))
                {
                    continue; // Skip invalid names
                }

                var filePath = Path.Combine(datasetFolder, fileName);
                
                if (System.IO.File.Exists(filePath))
                {
                    var zipEntry = archive.CreateEntry(fileName);
                    using var entryStream = zipEntry.Open();
                    using var fileStream = System.IO.File.OpenRead(filePath);
                    fileStream.CopyTo(entryStream);
                }
            }
        }

        memoryStream.Position = 0;
        return File(memoryStream, "application/zip", $"AI_Dataset_Selected_{DateTime.UtcNow:yyyyMMddHHmmss}.zip");
    }

    [HttpGet("file/{fileName}")]
    public IActionResult GetFile(string fileName)
    {
        // Prevent directory traversal
        if (string.IsNullOrEmpty(fileName) || fileName.Contains("..") || fileName.Contains("/") || fileName.Contains("\\"))
        {
            return BadRequest("Invalid file name.");
        }

        var datasetFolder = Path.Combine(_webHostEnvironment.WebRootPath, "AI-Dataset");
        var filePath = Path.Combine(datasetFolder, fileName);

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        var extension = Path.GetExtension(filePath).ToLower();
        var contentType = extension switch
        {
            ".pdf" => "application/pdf",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };

        return PhysicalFile(filePath, contentType);
    }
}
