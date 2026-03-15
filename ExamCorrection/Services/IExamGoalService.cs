using ExamCorrection.Entities;

namespace ExamCorrection.Services;

public interface IExamGoalService
{
    Task<Result<IEnumerable<ExamGoal>>> GetByExamIdAsync(int examId);
    Task<Result> SaveGoalsAsync(int examId, IEnumerable<ExamGoal> goals, bool isPartial = false);
    Task<Result<ExamGoal>> AddGoalAsync(ExamGoal goal);
    Task<Result> DeleteGoalAsync(int goalId);
}
