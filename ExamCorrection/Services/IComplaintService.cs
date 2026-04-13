using ExamCorrection.Contracts;
using ExamCorrection.Contracts.Complaints;

namespace ExamCorrection.Services;

public interface IComplaintService
{
    Task<Result> CreateComplaintAsync(CreateComplaintRequest request, CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<ComplaintResponse>>> GetAllComplaintsAsync(CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<ComplaintResponse>>> GetMyComplaintsAsync(CancellationToken cancellationToken = default);
    Task<Result> ResolveComplaintAsync(ResolveComplaintRequest request, CancellationToken cancellationToken = default);
}
