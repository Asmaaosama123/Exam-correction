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
        if (!result.IsSuccess) return result.ToProblem();

        var dtos = result.Value.Select(g => new
        {
            Id = g.Id,
            ExamId = g.ExamId,
            GoalText = g.GoalText,
            QuestionNumbers = g.QuestionNumbers
        });

        return Ok(dtos);
    }

    [HttpPost("{examId}")]
    public async Task<IActionResult> SaveGoals(int examId, [FromBody] IEnumerable<ExamGoalDto> goalsDto)
    {
        var goals = goalsDto.Select(dto => new ExamGoal
        {
            GoalText = dto.GoalText,
            QuestionNumbers = dto.QuestionNumbers
        });

        var result = await _goalService.SaveGoalsAsync(examId, goals);
        return result.IsSuccess ? Ok() : result.ToProblem();
    }
}

public class ExamGoalDto
{
    public string GoalText { get; set; } = string.Empty;
    public string QuestionNumbers { get; set; } = string.Empty;
}
