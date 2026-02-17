using ExamCorrection.Contracts.Grading;
using ExamCorrection.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExamCorrection.Controllers
{
    [ApiController]
    [Route("api/Grading")]
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
            string? searchValue = null)
        {
            var results = await _gradingService.GetGradingResultsAsync(pageNumber, pageSize, examId, classId, searchValue);
            return Ok(results);
        }
    }
}
