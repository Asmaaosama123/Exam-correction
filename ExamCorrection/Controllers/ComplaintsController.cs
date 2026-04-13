using ExamCorrection.Contracts;
using ExamCorrection.Contracts.Complaints;
using ExamCorrection.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamCorrection.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ComplaintsController(IComplaintService complaintService) : ControllerBase
{
    private readonly IComplaintService _complaintService = complaintService;

    [HttpPost]
    public async Task<IActionResult> CreateComplaint([FromBody] CreateComplaintRequest request, CancellationToken cancellationToken)
    {
        var result = await _complaintService.CreateComplaintAsync(request, cancellationToken);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllComplaints(CancellationToken cancellationToken)
    {
        var result = await _complaintService.GetAllComplaintsAsync(cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyComplaints(CancellationToken cancellationToken)
    {
        var result = await _complaintService.GetMyComplaintsAsync(cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPut("resolve")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Resolve([FromBody] ResolveComplaintRequest request, CancellationToken cancellationToken)
    {
        var result = await _complaintService.ResolveComplaintAsync(request, cancellationToken);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }
}
