using ExamCorrection.Contracts.Admin;
using ExamCorrection.Abstractions;
using ExamCorrection.Entities;

namespace ExamCorrection.Services;

public class AdminService(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext dbContext) : IAdminService
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly ApplicationDbContext _dbContext = dbContext;

    public async Task<Result<AdminStatsResponse>> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var totalUsers = await _userManager.Users.CountAsync(cancellationToken);
        
        var totalPages = await _dbContext.StudentExamPages
            .IgnoreQueryFilters()
            .CountAsync(cancellationToken);

        var totalSubscribers = await _userManager.Users
            .CountAsync(u => u.IsSubscribed || (u.SubscriptionExpiryUtc != null && u.SubscriptionExpiryUtc > DateTime.UtcNow), cancellationToken);

        return Result.Success(new AdminStatsResponse(totalUsers, totalPages, totalSubscribers));
    }

    public async Task<Result<AdminAdvancedStatsResponse>> GetAdvancedStatsAsync(CancellationToken cancellationToken = default)
    {
        var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
        var revenueQuery = await _dbContext.SubscriptionRequests
            .Include(r => r.Plan)
            .Where(r => r.Status == "Approved" && r.ProcessedAt >= sixMonthsAgo)
            .ToListAsync(cancellationToken);

        var revenueData = revenueQuery
            .GroupBy(r => r.ProcessedAt?.ToString("MMM yyyy") ?? "Unknown")
            .Select(g => new ChartDataPoint(g.Key, (double)g.Sum(r => r.Plan?.Price ?? 0)))
            .ToList();

        var popularPlansQuery = await _dbContext.SubscriptionRequests
            .Include(r => r.Plan)
            .Where(r => r.Status == "Approved" && r.Plan != null)
            .GroupBy(r => r.Plan!.Name)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var popularPlansData = popularPlansQuery
            .Select(x => new ChartDataPoint(x.Name, x.Count))
            .ToList();

        var totalUsers = await _userManager.Users.CountAsync(cancellationToken);
        var totalSubscribers = await _userManager.Users
            .CountAsync(u => u.IsSubscribed || (u.SubscriptionExpiryUtc != null && u.SubscriptionExpiryUtc > DateTime.UtcNow), cancellationToken);
        
        var subscriptionStatusData = new List<ChartDataPoint>
        {
            new("مشتركين", totalSubscribers),
            new("غير مشتركين", totalUsers - totalSubscribers)
        };

        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
        var activityQuery = await _dbContext.StudentExamPages
            .IgnoreQueryFilters()
            .Where(p => p.StudentExamPaper.GeneratedAt >= sevenDaysAgo)
            .Select(p => p.StudentExamPaper.GeneratedAt)
            .ToListAsync(cancellationToken);

        var examActivityData = activityQuery
            .GroupBy(d => d.ToString("MMM dd"))
            .Select(g => new ChartDataPoint(g.Key, g.Count()))
            .OrderBy(x => x.Label)
            .ToList();

        return Result.Success(new AdminAdvancedStatsResponse(
            revenueData,
            popularPlansData,
            subscriptionStatusData,
            examActivityData
        ));
    }

    public async Task<Result<IEnumerable<UserDto>>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userManager.Users.ToListAsync(cancellationToken);
        
        var pageCounts = await _dbContext.StudentExamPages
            .IgnoreQueryFilters()
            .GroupBy(p => p.StudentExamPaper.OwnerId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var dtos = users.Select(u =>
        {
            var correctedCount = pageCounts.FirstOrDefault(pc => pc.UserId == u.Id)?.Count ?? 0;
            return new UserDto(
                Id: u.Id,
                FirstName: u.FirstName ?? string.Empty,
                LastName: u.LastName ?? string.Empty,
                Email: u.Email ?? string.Empty,
                PhoneNumber: u.PhoneNumber ?? string.Empty,
                IsDisabled: u.IsDisabled,
                MaxAllowedPages: u.MaxAllowedPages,
                UsedPages: u.UsedPages,
                SubscriptionExpiryUtc: u.SubscriptionExpiryUtc,
                IsSubscribed: u.IsSubscribed,
                CorrectedPagesCount: correctedCount
            );
        });

        return Result.Success(dtos);
    }

    public async Task<Result<UserDto>> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) && string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return Result.Failure<UserDto>(new Error("Admin.MissingIdentifier", "Email or Phone Number is required.", StatusCodes.Status400BadRequest));
        }

        var user = new ApplicationUser
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            UserName = !string.IsNullOrWhiteSpace(request.Email) ? request.Email : request.PhoneNumber,
            PhoneNumber = request.PhoneNumber,
            IsDisabled = false
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            var firstError = result.Errors.FirstOrDefault();
            return Result.Failure<UserDto>(new Error("Admin.CreateUserFailed", firstError?.Description ?? "Failed to create user.", StatusCodes.Status400BadRequest));
        }

        return Result.Success(new UserDto(
            Id: user.Id,
            FirstName: user.FirstName,
            LastName: user.LastName,
            Email: user.Email ?? string.Empty,
            PhoneNumber: user.PhoneNumber ?? string.Empty,
            IsDisabled: user.IsDisabled,
            CorrectedPagesCount: 0
        ));
    }

    public async Task<Result<UserDto>> UpdateUserAsync(string userId, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return Result.Failure<UserDto>(new Error("Admin.UserNotFound", "User not found.", StatusCodes.Status404NotFound));
        }

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.PhoneNumber = request.PhoneNumber;
        user.IsDisabled = request.IsDisabled;
        user.MaxAllowedPages = request.MaxAllowedPages;
        user.SubscriptionExpiryUtc = request.SubscriptionExpiryUtc;
        user.IsSubscribed = request.IsSubscribed;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return Result.Failure<UserDto>(new Error("Admin.UpdateUserFailed", "Failed to update user.", StatusCodes.Status400BadRequest));
        }

        return Result.Success(new UserDto(
            Id: user.Id,
            FirstName: user.FirstName,
            LastName: user.LastName,
            Email: user.Email ?? string.Empty,
            PhoneNumber: user.PhoneNumber ?? string.Empty,
            IsDisabled: user.IsDisabled,
            MaxAllowedPages: user.MaxAllowedPages,
            UsedPages: user.UsedPages,
            SubscriptionExpiryUtc: user.SubscriptionExpiryUtc,
            IsSubscribed: user.IsSubscribed,
            CorrectedPagesCount: 0 // Will be recalculated on next list
        ));
    }

    public async Task<Result> DeleteUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return Result.Failure(new Error("Admin.UserNotFound", "User not found.", StatusCodes.Status404NotFound));
        }

        var result = await _userManager.DeleteAsync(user);

        if (!result.Succeeded)
        {
            return Result.Failure(new Error("Admin.DeleteUserFailed", "Failed to delete user. There might be related records.", StatusCodes.Status400BadRequest));
        }

        return Result.Success();
    }

    public async Task<Result<IEnumerable<TeacherExamSummaryDto>>> GetTeacherExamsAsync(string teacherId, CancellationToken cancellationToken = default)
    {
        var data = await _dbContext.Exams
            .Select(e => new 
            {
                e.Id,
                e.Title,
                e.Subject,
                PaperCount = _dbContext.StudentExamPapers.Count(p => p.ExamId == e.Id && p.OwnerId == teacherId),
                LastCorrectedAt = _dbContext.StudentExamPapers
                    .Where(p => p.ExamId == e.Id && p.OwnerId == teacherId)
                    .Max(p => (DateTime?)p.GeneratedAt)
            })
            .OrderByDescending(x => x.LastCorrectedAt)
            .ToListAsync(cancellationToken);

        var summaries = data.Select(s => new TeacherExamSummaryDto(
            s.Id,
            s.Title,
            s.Subject,
            s.PaperCount,
            s.LastCorrectedAt ?? DateTime.MinValue
        ));

        return Result.Success<IEnumerable<TeacherExamSummaryDto>>(summaries);
    }

    public async Task<Result<Dictionary<string, string>>> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.SystemSettings.ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);
        return Result.Success(settings);
    }

    public async Task<Result> UpdateSettingAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var setting = await _dbContext.SystemSettings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        if (setting == null)
        {
            setting = new SystemSetting { Key = key, Value = value };
            _dbContext.SystemSettings.Add(setting);
        }
        else
        {
            setting.Value = value;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<bool> IsSubscriptionRequiredAsync(CancellationToken cancellationToken = default)
    {
        var setting = await _dbContext.SystemSettings.FirstOrDefaultAsync(s => s.Key == "IsSubscriptionRequired", cancellationToken);
        return setting?.Value?.ToLower() == "true";
    }
}
