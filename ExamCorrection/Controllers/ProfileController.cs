namespace ExamCorrection.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ProfileController(IProfileService profileService) : ControllerBase
{
    private readonly IProfileService _profileService = profileService;

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var result = await _profileService.GetCurrentUser();
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}