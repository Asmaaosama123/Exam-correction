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

    public async Task<Result> SaveGoalsAsync(int examId, IEnumerable<ExamGoal> goals, bool isPartial = false)
    {
        var existingGoals = await _context.ExamGoals
            .Where(g => g.ExamId == examId)
            .ToListAsync();

        var userId = _userContext.UserId!;
        var updatedIds = new HashSet<int>();

        foreach (var incomingGoal in goals)
        {
            var trimmedIncoming = (incomingGoal.GoalText ?? "").Trim();
            if (string.IsNullOrWhiteSpace(trimmedIncoming)) continue;
            
            // Try matching: 
            // 1. By ID if provided
            // 2. By Text (case-insensitive) if ID is 0 or not found
            var existing = (incomingGoal.Id > 0) 
                ? existingGoals.FirstOrDefault(g => g.Id == incomingGoal.Id)
                : existingGoals.FirstOrDefault(g => !updatedIds.Contains(g.Id) && string.Equals((g.GoalText ?? "").Trim(), trimmedIncoming, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                // Update existing
                existing.GoalText = trimmedIncoming;
                existing.QuestionNumbers = incomingGoal.QuestionNumbers ?? "";
                updatedIds.Add(existing.Id);
            }
            else
            {
                // New goal
                var newGoal = new ExamGoal
                {
                    ExamId = examId,
                    GoalText = trimmedIncoming,
                    QuestionNumbers = incomingGoal.QuestionNumbers ?? "",
                    OwnerId = userId
                };
                _context.ExamGoals.Add(newGoal);
            }
        }

        // Delete any existing goals that were not updated or mentioned in the incoming set
        if (!isPartial)
        {
            var toDelete = existingGoals.Where(eg => !updatedIds.Contains(eg.Id)).ToList();
            if (toDelete.Any())
            {
                _context.ExamGoals.RemoveRange(toDelete);
            }
        }

        await _context.SaveChangesAsync();
        return Result.Success();
    }
    public async Task<Result<ExamGoal>> AddGoalAsync(ExamGoal goal)
    {
        var userId = _userContext.UserId!;
        goal.OwnerId = userId;
        
        _context.ExamGoals.Add(goal);
        await _context.SaveChangesAsync();
        
        return Result.Success(goal);
    }

    public async Task<Result> DeleteGoalAsync(int goalId)
    {
        var goal = await _context.ExamGoals.FindAsync(goalId);
        if (goal == null) return Result.Failure(new Error("ExamGoal.NotFound", "الهدف غير موجود", 404));

        _context.ExamGoals.Remove(goal);
        await _context.SaveChangesAsync();
        
        return Result.Success();
    }
}
