namespace ExamCorrection.Services;

public class ClassService(ApplicationDbContext context) : IClassService
{
    private readonly ApplicationDbContext _context = context;

    public async Task<Result<ClassResponse>> GetAsync(int classId, CancellationToken cancellationToken = default)
    {
        if (!await _context.Classes.AnyAsync(x => x.Id == classId, cancellationToken))
            return Result.Failure<ClassResponse>(ClassErrors.ClassNotFound);

        var response = await _context.Classes
            .Where(x => x.Id == classId)
            .ProjectToType<ClassResponse>()
            .SingleOrDefaultAsync(cancellationToken);

        if (response is null)
            return Result.Failure<ClassResponse>(ClassErrors.ClassNotFound);

        return Result.Success(response);
    }

    public async Task<Result<IEnumerable<ClassResponse>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var response = await _context.Classes
            .ProjectToType<ClassResponse>()
            .ToListAsync(cancellationToken);

        return Result.Success<IEnumerable<ClassResponse>>(response);
    }

    public async Task<Result<ClassResponse>> AddAsync(ClassRequest request, CancellationToken cancellationToken = default)
    {
        var isExistingClassName = await _context.Classes.AnyAsync(c => c.Name == request.Name, cancellationToken);

        if (isExistingClassName)
            return Result.Failure<ClassResponse>(ClassErrors.DuplicatedClassName);

        var cls = request.Adapt<Class>();

        await _context.Classes.AddAsync(cls, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success<ClassResponse>(cls.Adapt<ClassResponse>());
    }

    public async Task<Result> UpdateAsync(int classId, ClassRequest request, CancellationToken cancellationToken = default)
    {
        var existingClass = await _context.Classes.SingleOrDefaultAsync(c => c.Id == classId, cancellationToken);

        if (existingClass is null)
            return Result.Failure<ClassResponse>(ClassErrors.ClassNotFound);

        var isExistingClassName = await _context.Classes.AnyAsync(c => c.Name == request.Name && c.Id != classId, cancellationToken);

        if (isExistingClassName)
            return Result.Failure<ClassResponse>(ClassErrors.DuplicatedClassName);

        existingClass.Name = request.Name;

        _context.Classes.Update(existingClass);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success("تم تعديل الفصل بنجاح.");
    }

    public async Task<Result> DeleteAsync(int classId, CancellationToken cancellationToken = default)
    {
        var existingClass = await _context.Classes.SingleOrDefaultAsync(c => c.Id == classId, cancellationToken);

        if (existingClass is null)
            return Result.Failure<ClassResponse>(ClassErrors.ClassNotFound);

        _context.Classes.Remove(existingClass);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success("تم حذف الفصل بنجاح.");
    }
}