using ExamCorrection.Abstractions;
using ExamCorrection.Entities;
using ExamCorrection.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamCorrection.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ExamGoalsController : ControllerBase
{
    private readonly IExamGoalService _goalService ;
    private readonly ILogger<ExamGoalsController> _logger; // تعريف الـ logger

  public ExamGoalsController( ILogger<ExamGoalsController> logger,IExamGoalService goalService)
    {
        _logger = logger; // تهيئة الـ logger
        _goalService = goalService;
    }
    
    [HttpGet("{examId}")]
    public async Task<IActionResult> GetByExamId(int examId)
    {
       try
       {
        var result = await _goalService.GetByExamIdAsync(examId);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
       }
       catch (Exception ex)
       {
        // Log ex
        return StatusCode(500, new { message = "خطأ داخلي في الخادم", details = ex.Message });
        }
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
   /*
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
    */
[HttpPost]
public async Task<IActionResult> CreateGoal([FromBody] ExamGoalDto dto)
{
    try
    {
        if (string.IsNullOrWhiteSpace(dto.GoalText))
        {
            _logger.LogWarning("Attempted to create a goal with empty GoalText for ExamId {ExamId}", dto.ExamId);
            return BadRequest(new { message = "الهدف لا يمكن أن يكون فارغاً" });
        }

        var goal = new ExamGoal
        {
            ExamId = dto.ExamId,
            GoalText = dto.GoalText,
            QuestionNumbers = dto.QuestionNumbers ?? ""
        };

        var result = await _goalService.AddGoalAsync(goal);

        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to add goal for ExamId {ExamId}.", dto.ExamId);
            return StatusCode(500, new { message = "حدث خطأ أثناء إضافة الهدف" });
        }

        _logger.LogInformation("Goal added successfully for ExamId {ExamId}, GoalId {GoalId}", dto.ExamId, result.Value?.Id);
        return Ok(result.Value);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Exception while creating goal for ExamId {ExamId}", dto.ExamId);
        return StatusCode(500, new { message = "خطأ داخلي في الخادم أثناء إضافة الهدف", details = ex.Message });
    }
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
