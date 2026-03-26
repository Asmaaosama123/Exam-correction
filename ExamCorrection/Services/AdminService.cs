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

        return Result.Success(new AdminStatsResponse(totalUsers, totalPages));
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
        var summaries = await _dbContext.StudentExamPapers
            .Include(p => p.Exam)
            .Where(p => p.OwnerId == teacherId)
            .GroupBy(p => new { p.ExamId, p.Exam.Title, p.Exam.Subject })
            .Select(g => new TeacherExamSummaryDto(
                g.Key.ExamId,
                g.Key.Title,
                g.Key.Subject,
                g.Count(),
                g.Max(p => p.GeneratedAt)
            ))
            .OrderByDescending(s => s.LastCorrectedAt)
            .ToListAsync(cancellationToken);

        return Result.Success<IEnumerable<TeacherExamSummaryDto>>(summaries);
    }
}
