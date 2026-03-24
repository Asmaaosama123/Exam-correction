using ExamCorrection.Entities;
using ExamCorrection.Persistance;
using Microsoft.EntityFrameworkCore;

namespace ExamCorrection.Services;

public class SystemLogService(ApplicationDbContext context) : ISystemLogService
{
    public async Task LogErrorAsync(string message, string details, string source, string? userId = null)
    {
        var log = new SystemErrorLog
        {
            ErrorMessage = message ?? "Unknown Error",
            ErrorDetails = details ?? string.Empty,
            ErrorSource = source ?? "SYSTEM",
            OwnerId = userId,
            CreatedAt = DateTime.UtcNow,
            IsResolved = false
        };

        context.SystemErrorLogs.Add(log);
        await context.SaveChangesAsync();
    }

    public async Task<IEnumerable<object>> GetErrorSummaryAsync()
    {
        // Group unresolved errors by message
        var summary = await context.SystemErrorLogs
            .AsNoTracking()
            .Where(e => !e.IsResolved)
            .GroupBy(e => e.ErrorMessage)
            .Select(g => new
            {
                ErrorMessage = g.Key,
                Count = g.Count(),
                LastOccurrence = g.Max(e => e.CreatedAt)
            })
            .OrderByDescending(g => g.Count)
            .ThenByDescending(g => g.LastOccurrence)
            .ToListAsync();

        return summary;
    }

    public async Task<IEnumerable<SystemErrorLog>> GetErrorDetailsAsync(string errorMessage)
    {
        return await context.SystemErrorLogs
            .AsNoTracking()
            .Include(e => e.User)
            .Where(e => e.ErrorMessage == errorMessage && !e.IsResolved)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> ResolveErrorAsync(string errorMessage)
    {
        var logs = await context.SystemErrorLogs
            .Where(e => e.ErrorMessage == errorMessage && !e.IsResolved)
            .ToListAsync();

        if (!logs.Any()) return false;

        foreach (var log in logs)
        {
            log.IsResolved = true;
        }

        await context.SaveChangesAsync();
        return true;
    }
}
