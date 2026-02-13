namespace ExamCorrection.Services;

public class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public string? UserId => _httpContextAccessor.HttpContext?.User.GetUserId();
}