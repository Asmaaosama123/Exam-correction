using ExamCorrection.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExamCorrection.Services;

public class ExamGoalService(ApplicationDbContext context, IUserContext userContext) : IExamGoalService
{
    private readonly ApplicationDbContext _context = context;
    private readonly IUserContext _userContext = userContext;

    public async Task<Result<IEnumerable<ExamGoal>>> GetByExamIdAsync(int examId)
    {
        var goals = await _context.ExamGoals
            .Where(g => g.ExamId == examId)
            .ToListAsync();
        
        return Result.Success<IEnumerable<ExamGoal>>(goals);
    }

    public async Task<Result> SaveGoalsAsync(int examId, IEnumerable<ExamGoal> goals)
    {
        var existingGoals = await _context.ExamGoals
            .Where(g => g.ExamId == examId)
            .ToListAsync();

        _context.ExamGoals.RemoveRange(existingGoals);

        foreach (var goal in goals)
        {
            goal.ExamId = examId;
            goal.OwnerId = _userContext.UserId!;
            _context.ExamGoals.Add(goal);
        }

        await _context.SaveChangesAsync();
        return Result.Success();
    }
}
