using ExamCorrection.Contracts.TutorialVideos;
using ExamCorrection.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamCorrection.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TutorialVideosController : ControllerBase
{
    private readonly ITutorialVideoService _tutorialVideoService;

    public TutorialVideosController(ITutorialVideoService tutorialVideoService)
    {
        _tutorialVideoService = tutorialVideoService;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll()
    {
        var result = await _tutorialVideoService.GetAllAsync();
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create([FromForm] CreateTutorialVideoRequest request)
    {
        var result = await _tutorialVideoService.CreateAsync(request);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _tutorialVideoService.DeleteAsync(id);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }
}
