using Microsoft.AspNetCore.Http;

namespace ExamCorrection.Contracts.TutorialVideos;

public record CreateTutorialVideoRequest(
    string Title,
    string? Description,
    IFormFile File
);

public record TutorialVideoResponse(
    int Id,
    string Title,
    string? Description,
    string VideoPath,
    DateTime CreatedAt
);
