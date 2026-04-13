using ExamCorrection.Abstractions;
using ExamCorrection.Contracts.TutorialVideos;

namespace ExamCorrection.Services;

public interface ITutorialVideoService
{
    Task<Result<IEnumerable<TutorialVideoResponse>>> GetAllAsync();
    Task<Result<TutorialVideoResponse>> CreateAsync(CreateTutorialVideoRequest request);
    Task<Result> DeleteAsync(int id);
}
