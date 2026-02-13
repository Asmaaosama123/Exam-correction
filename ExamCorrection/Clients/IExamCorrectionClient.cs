using ExamCorrection.Contracts;
using Refit;

namespace ExamCorrection.Clients;

public interface IExamCorrectionClient
{
    [Multipart]
    [Post("/mcq")]
    Task<ExamApiResponse> ProcessExamAsync([AliasAs("files")] StreamPart file, CancellationToken cancellationToken);
}