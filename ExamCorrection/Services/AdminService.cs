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
        
        var dtos = users.Select(u => new UserDto(
            Id: u.Id,
            FirstName: u.FirstName ?? string.Empty,
            LastName: u.LastName ?? string.Empty,
            Email: u.Email ?? string.Empty,
            PhoneNumber: u.PhoneNumber ?? string.Empty,
            IsDisabled: u.IsDisabled
        ));

        return Result.Success(dtos);
    }

    public async Task<Result<UserDto>> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = new ApplicationUser
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            UserName = request.Email,
            PhoneNumber = request.PhoneNumber,
            IsDisabled = false
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            return Result.Failure<UserDto>(new Error("Admin.CreateUserFailed", "Failed to create user.", StatusCodes.Status400BadRequest));
        }

        return Result.Success(new UserDto(
            Id: user.Id,
            FirstName: user.FirstName,
            LastName: user.LastName,
            Email: user.Email ?? string.Empty,
            PhoneNumber: user.PhoneNumber ?? string.Empty,
            IsDisabled: user.IsDisabled
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
            IsDisabled: user.IsDisabled
        ));
    }

    public async Task<Result> DeleteUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return Result.Failure(new Error("Admin.UserNotFound", "User not found.", StatusCodes.Status404NotFound));
        }

        // Instead of hard delete, maybe just disable them. The admin wants "Delete" (حذف).
        // If we delete, it might violate FK constraints unless cascade is configured.
        // I will do hard delete, if DbContext allows it, otherwise just disable.
        // The user says "تعديل حذف إضافة دخول" (Edit, Delete, Add, Login).
        var result = await _userManager.DeleteAsync(user);

        if (!result.Succeeded)
        {
            return Result.Failure(new Error("Admin.DeleteUserFailed", "Failed to delete user. There might be related records.", StatusCodes.Status400BadRequest));
        }

        return Result.Success();
    }
}
