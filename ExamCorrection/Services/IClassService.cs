namespace ExamCorrection.Services;

public interface IClassService
{
    Task<Result<ClassResponse>> GetAsync(int classId, CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<ClassResponse>>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<ClassResponse>> AddAsync(ClassRequest request, CancellationToken cancellationToken = default);
    Task<Result> UpdateAsync(int classId, ClassRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(int classId, CancellationToken cancellationToken = default);
}