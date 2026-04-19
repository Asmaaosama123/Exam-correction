using ExamCorrection.Entities;
using ExamCorrection.Persistance;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ExamCorrection.Services;

public class ProfileService(UserManager<ApplicationUser> userManager, IHttpContextAccessor httpContextAccessor, ApplicationDbContext dbContext) : IProfileService
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly ApplicationDbContext _dbContext = dbContext;

    public async Task<Result<CurrentUserResponse>> GetCurrentUser()
    {
        var httpContext = _httpContextAccessor.HttpContext;

        var userId = httpContext?.User.GetUserId();

        if (userId is null)
            return Result.Failure<CurrentUserResponse>(UserErrors.InvalidJwtToken);

        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
            return Result.Failure<CurrentUserResponse>(UserErrors.InvalidJwtToken);

        var userRoles = await _userManager.GetRolesAsync(user);

        var subSetting = await _dbContext.SystemSettings.FirstOrDefaultAsync(s => s.Key == "IsSubscriptionRequired");
        var isSubscriptionEnabled = subSetting?.Value == "true";

        var response = new CurrentUserResponse(
            user.Id,
            user.FirstName ?? "",
            user.LastName ?? "",
            userRoles,
            user.MaxAllowedPages,
            user.UsedPages,
            user.IsSubscribed,
            user.SubscriptionExpiryUtc,
            isSubscriptionEnabled
        );

        return Result.Success(response);
    }
}