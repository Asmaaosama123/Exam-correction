using ExamCorrection.Contracts.Grading;
using ExamCorrection.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExamCorrection.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class GradingResultsController : ControllerBase
    {
        private readonly GradingService _gradingService;

        public GradingResultsController(GradingService gradingService)
        {
            _gradingService = gradingService;
        }

        [HttpGet]
        public async Task<ActionResult<GradingResultsResponse>> Get(
            int pageNumber = 1,
            int pageSize = 10,
            int? examId = null,
            int? classId = null,
            string? searchValue = null,
            string? teacherId = null)
        {
            var results = await _gradingService.GetGradingResultsAsync(pageNumber, pageSize, examId, classId, searchValue, teacherId);
            return Ok(results);
        [HttpPost("{id}/manual-update")]
        public async Task<IActionResult> UpdateManualGrading(int id, [FromBody] List<ManualCorrectionDto> corrections)
        {
            var success = await _gradingService.UpdateManualGradingAsync(id, corrections);
            return success ? Ok() : BadRequest("تعذر تحديث البيانات");
        }
    }
}
