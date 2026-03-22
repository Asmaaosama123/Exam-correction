using ExamCorrection.Contracts;
using ExamCorrection.Contracts.Complaints;
using ExamCorrection.Entities;
using ExamCorrection.Persistance;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace ExamCorrection.Services;

public class ComplaintService(ApplicationDbContext context, IUserContext userContext) : IComplaintService
{
    private readonly ApplicationDbContext _context = context;
    private readonly IUserContext _userContext = userContext;

    public async Task<Result> CreateComplaintAsync(CreateComplaintRequest request, CancellationToken cancellationToken = default)
    {
        var complaint = new Complaint
        {
            Message = request.Message,
            CreatedAt = DateTime.Now,
            OwnerId = _userContext.UserId!
        };

        _context.Complaints.Add(complaint);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<IEnumerable<ComplaintResponse>>> GetAllComplaintsAsync(CancellationToken cancellationToken = default)
    {
        var complaints = await _context.Complaints
            .Include(c => c.User)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new ComplaintResponse(
                c.Id,
                c.Message,
                c.CreatedAt,
                c.User.FirstName + " " + c.User.LastName
            ))
            .ToListAsync(cancellationToken);

        return Result.Success<IEnumerable<ComplaintResponse>>(complaints);
    }
}
