using ExamCorrection.Contracts;
using ExamCorrection.Contracts.Complaints;

namespace ExamCorrection.Services;

public interface IComplaintService
{
    Task<Result> CreateComplaintAsync(CreateComplaintRequest request, CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<ComplaintResponse>>> GetAllComplaintsAsync(CancellationToken cancellationToken = default);
}
