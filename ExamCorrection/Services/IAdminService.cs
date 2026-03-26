using ExamCorrection.Contracts.Admin;
using ExamCorrection.Abstractions; // Assuming Result is here

namespace ExamCorrection.Services;

public interface IAdminService
{
    Task<Result<AdminStatsResponse>> GetStatsAsync(CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<UserDto>>> GetAllUsersAsync(CancellationToken cancellationToken = default);
    Task<Result<UserDto>> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<Result<UserDto>> UpdateUserAsync(string userId, UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<TeacherExamSummaryDto>>> GetTeacherExamsAsync(string teacherId, CancellationToken cancellationToken = default);
}
