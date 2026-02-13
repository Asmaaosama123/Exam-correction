namespace ExamCorrection.Services;

public class ProfileService(UserManager<ApplicationUser> userManager, IHttpContextAccessor httpContextAccessor) : IProfileService
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public async Task<Result<CurrentUserResponse>> GetCurrentUser()
    {
        var httpContext = _httpContextAccessor.HttpContext;

        var userId = httpContext?.User.GetUserId();

        if (userId is null)
            return Result.Failure<CurrentUserResponse>(UserErrors.InvalidJwtToken);

        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
            return Result.Failure<CurrentUserResponse>(UserErrors.InvalidJwtToken);

        var response = new CurrentUserResponse(
            user.Id,
            user.FirstName,
            user.LastName
        );

        return Result.Success(response);
    }
}