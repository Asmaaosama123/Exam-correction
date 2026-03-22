namespace ExamCorrection.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class StudentsController(IStudentServices studentServices) : ControllerBase
{
    private readonly IStudentServices _studentServices = studentServices;

    [HttpGet("")]
    public async Task<IActionResult> GetAll([FromQuery] int? classId, [FromQuery] RequestFilters filters, CancellationToken cancellationToken)
    {
        var result = await _studentServices.GetAllAsync(classId, filters, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("{studentId}")]
    public async Task<IActionResult> Get([FromQuery] int? classId, int studentId, CancellationToken cancellationToken)
    {
        var result = await _studentServices.GetAsync(classId, studentId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost("")]
    public async Task<IActionResult> Create([FromQuery] int classId, [FromBody] StudentRequest request, CancellationToken cancellationToken)
    {
        var result = await _studentServices.AddAsync(classId, request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { classId, studentId = result.Value!.Id }, result.Value)
            : result.ToProblem();
    }

    [HttpPut("{studentId}")]
    public async Task<IActionResult> Update(int studentId, [FromBody] UpdateStudentRequest request, CancellationToken cancellationToken)
    {
        var result = await _studentServices.UpdateAsync(studentId, request, cancellationToken);
        return result.IsSuccess ? NoContent() : result.ToProblem();
    }

    [HttpDelete("{studentId}")]
    public async Task<IActionResult> Delete(int studentId, CancellationToken cancellationToken)
    {
        var result = await _studentServices.Delete(studentId, cancellationToken);
        return result.IsSuccess ? NoContent() : result.ToProblem();
    }

    [HttpPost("import-students")]
    public async Task<IActionResult> Import([FromForm] BulkImportFileRequest request, CancellationToken cancellationToken)
    {
        var result = await _studentServices.ImportStudentsAsync(request.File,request.ClassId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}