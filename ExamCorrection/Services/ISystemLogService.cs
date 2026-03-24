using ExamCorrection.Entities;

namespace ExamCorrection.Services;

public interface ISystemLogService
{
    Task LogErrorAsync(string message, string details, string source, string? userId = null);
    Task<IEnumerable<object>> GetErrorSummaryAsync();
    Task<IEnumerable<SystemErrorLog>> GetErrorDetailsAsync(string errorMessage);
    Task<bool> ResolveErrorAsync(string errorMessage);
}
