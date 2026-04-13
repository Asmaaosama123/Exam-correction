using ExamCorrection.Abstractions;
using ExamCorrection.Contracts.TutorialVideos;
using ExamCorrection.Entities;
using ExamCorrection.Persistance;
using Mapster;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace ExamCorrection.Services;

public class TutorialVideoService : ITutorialVideoService
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public TutorialVideoService(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
    {
        _context = context;
        _webHostEnvironment = webHostEnvironment;
    }

    public async Task<Result<IEnumerable<TutorialVideoResponse>>> GetAllAsync()
    {
        var videos = await _context.TutorialVideos
            .OrderByDescending(v => v.CreatedAt)
            .ProjectToType<TutorialVideoResponse>()
            .ToListAsync();

        return Result.Success<IEnumerable<TutorialVideoResponse>>(videos);
    }

    public async Task<Result<TutorialVideoResponse>> CreateAsync(CreateTutorialVideoRequest request)
    {
        if (request.File == null)
            return Result.Failure<TutorialVideoResponse>(new Error("TutorialVideo.NoFile", "يرجى اختيار ملف فيديو للرفع.", 400));

        // Allowed video extensions
        var allowedExtensions = new[] { ".mp4", ".mov", ".avi", ".mkv" };
        var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
            return Result.Failure<TutorialVideoResponse>(new Error("TutorialVideo.InvalidFormat", "صيغة الفيديو غير مدعومة. يرجى اختيار ملف MP4 أو MOV أو AVI.", 400));

        try
        {
            var folder = Path.Combine(_webHostEnvironment.WebRootPath, "Uploads", "TutorialVideos");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var fileName = $"{Guid.NewGuid()}{extension}";
            var fullPath = Path.Combine(folder, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await request.File.CopyToAsync(stream);
            }

            var video = new TutorialVideo
            {
                Title = request.Title,
                Description = request.Description,
                VideoPath = $"Uploads/TutorialVideos/{fileName}",
                CreatedAt = DateTime.UtcNow
            };

            _context.TutorialVideos.Add(video);
            await _context.SaveChangesAsync();

            return Result.Success(video.Adapt<TutorialVideoResponse>());
        }
        catch (Exception ex)
        {
            return Result.Failure<TutorialVideoResponse>(new Error("TutorialVideo.UploadError", $"حدث خطأ أثناء رفع الفيديو: {ex.Message}", 500));
        }
    }

    public async Task<Result> DeleteAsync(int id)
    {
        var video = await _context.TutorialVideos.FindAsync(id);
        if (video == null)
            return Result.Failure(new Error("TutorialVideo.NotFound", "الفيديو غير موجود.", 404));

        try
        {
            // Delete physical file
            var fullPath = Path.Combine(_webHostEnvironment.WebRootPath, video.VideoPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            _context.TutorialVideos.Remove(video);
            await _context.SaveChangesAsync();

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error("TutorialVideo.DeleteError", $"حدث خطأ أثناء حذف الفيديو: {ex.Message}", 500));
        }
    }
}
