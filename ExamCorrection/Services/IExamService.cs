using ExamCorrection.Contracts.TeacherExam;
using ExamCorrection.Contracts.AI;

namespace ExamCorrection.Services;

public interface IExamService
{
    Task<Result<IEnumerable<ExamResponse>>> GetAllAsync();
    Task<Result<ExamResponse>> GetAsync(int examId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(int examId, CancellationToken cancellationToken = default);
    Task<Result> UploadExamPdfAsync(UploadExamRequest request);
    Task<Result<FileExamResponse>> GenerateAndDownloadExamsAsync(GenerateExamRequest request);
    Task<Result<TeacherExamResponse>> UploadTeacherExamAsync(UploadTeacherExamRequest request);

    Task<Result<List<ExamCorrectionResponse>>> UploadAndSaveExamAnswersAsync(IFormFile file, CancellationToken cancellationToken);


}
