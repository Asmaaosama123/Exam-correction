namespace ExamCorrection.Services;

public interface IStudentServices
{
    Task<Result<PaginatedList<StudentResponse>>> GetAllAsync(int? classId, RequestFilters filters, CancellationToken cancellationToken = default);
    Task<Result<StudentResponse>> GetAsync(int? classId, int studentId, CancellationToken cancellationToken = default);
    Task<Result<StudentResponse>> AddAsync(int classId, StudentRequest request, CancellationToken cancellationToken = default);
    Task<Result> UpdateAsync(int studentId, UpdateStudentRequest request, CancellationToken cancellationToken = default);
    Task<Result> Delete(int studentId, CancellationToken cancellationToken = default);
    Task<Result<BulkImportFileResponse>> ImportStudentsAsync(IFormFile file, int classId, CancellationToken cancellationToken = default);
}