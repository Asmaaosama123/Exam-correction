namespace ExamCorrection.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ClassesController(IClassService classService) : ControllerBase
{
    private readonly IClassService _classService = classService;

    [HttpGet("")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _classService.GetAllAsync(cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("{classId}")]
    public async Task<IActionResult> Get(int classId, CancellationToken cancellationToken)
    {
        var result = await _classService.GetAsync(classId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost("")]
    public async Task<IActionResult> Create([FromBody] ClassRequest request, CancellationToken cancellationToken)
    {
        var result = await _classService.AddAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { classId = result.Value!.Id}, result.Value)
            : result.ToProblem();
    }

    [HttpPut("{classId}")]
    public async Task<IActionResult> Update(int classId, [FromBody] ClassRequest request, CancellationToken cancellationToken)
    {
        var result = await _classService.UpdateAsync(classId, request, cancellationToken);
        return result.IsSuccess ? NoContent() : result.ToProblem();
    }

    [HttpDelete("{classId}")]
    public async Task<IActionResult> Delete(int classId, CancellationToken cancellationToken)
    {
        var result = await _classService.DeleteAsync(classId, cancellationToken);
        return result.IsSuccess ? NoContent() : result.ToProblem();
    }
}