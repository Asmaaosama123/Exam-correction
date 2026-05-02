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
            string? teacherId = null,
            bool? onlyAnonymous = null)
        {
            var results = await _gradingService.GetGradingResultsAsync(pageNumber, pageSize, examId, classId, searchValue, teacherId, onlyAnonymous);
            return Ok(results);
        }

        [HttpPost("{id}/manual-update")]
        public async Task<IActionResult> UpdateManualGrading(int id, [FromBody] ManualUpdateRequest request)
        {
            var success = await _gradingService.UpdateManualGradingAsync(id, request.Corrections, request.StudentId);
            return success ? Ok() : BadRequest("تعذر تحديث البيانات");
        }
    }

    public class ManualUpdateRequest
    {
        public List<ManualCorrectionDto> Corrections { get; set; } = new();
        public int? StudentId { get; set; }
    }
}
