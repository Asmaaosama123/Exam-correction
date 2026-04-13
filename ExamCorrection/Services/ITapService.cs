using ExamCorrection.Abstractions;
using ExamCorrection.Clients.Tap;

namespace ExamCorrection.Services;

public interface ITapService
{
    Task<Result<ChargeResponse>> CreateChargeAsync(CreateChargeRequest request, CancellationToken cancellationToken = default);
    Task<Result<ChargeResponse>> GetChargeStatusAsync(string chargeId, CancellationToken cancellationToken = default);
}
