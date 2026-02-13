using ExamCorrection.Contracts.Profile;

namespace ExamCorrection.Services;

public interface IProfileService
{
    Task<Result<CurrentUserResponse>> GetCurrentUser();
}