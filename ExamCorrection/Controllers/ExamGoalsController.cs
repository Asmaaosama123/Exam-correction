using ExamCorrection.Abstractions;
using ExamCorrection.Entities;
using ExamCorrection.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamCorrection.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ExamGoalsController(IExamGoalService goalService) : ControllerBase
{
    private readonly IExamGoalService _goalService = goalService;

    [HttpGet("{examId}")]
    public async Task<IActionResult> GetByExamId(int examId)
    {
        var result = await _goalService.GetByExamIdAsync(examId);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost("{examId}")]
    public async Task<IActionResult> SaveGoals(int examId, [FromBody] IEnumerable<ExamGoalDto> goalsDto, [FromQuery] bool isPartial = false)
    {
        var goals = goalsDto.Select(dto => new ExamGoal
        {
            Id = dto.Id ?? 0,
            ExamId = examId,
            GoalText = dto.GoalText,
            QuestionNumbers = dto.QuestionNumbers ?? ""
        });

        var result = await _goalService.SaveGoalsAsync(examId, goals, isPartial);
        return result.IsSuccess ? Ok() : result.ToProblem();
    }

    [HttpPost]
    public async Task<IActionResult> CreateGoal([FromBody] ExamGoalDto dto)
    {
        var goal = new ExamGoal
        {
            ExamId = dto.ExamId,
            GoalText = dto.GoalText,
            QuestionNumbers = dto.QuestionNumbers ?? ""
        };

        var result = await _goalService.AddGoalAsync(goal);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteGoal(int id)
    {
        var result = await _goalService.DeleteGoalAsync(id);
        return result.IsSuccess ? Ok() : result.ToProblem();
    }
}

public class ExamGoalDto
{
    public int? Id { get; set; }
    public int ExamId { get; set; }
    public string GoalText { get; set; } = string.Empty;
    public string QuestionNumbers { get; set; } = string.Empty;
}
