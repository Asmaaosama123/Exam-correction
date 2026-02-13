namespace ExamCorrection.Persistance;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IUserContext userContext) :
    IdentityDbContext<ApplicationUser, ApplicationRole, string>(options)
{
    private readonly IUserContext _userContext = userContext;

    public DbSet<Class> Classes { set; get; }
    public DbSet<Student> Students { set; get; }
    public DbSet<Exam> Exams { set; get; }
    public DbSet<ExamPage> ExamPages { set; get; }
    public DbSet<StudentExamPaper> StudentExamPapers { set; get; }
    public DbSet<StudentExamPage> StudentExamPages { set; get; }
    public DbSet<TeacherExam> TeacherExams { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        builder.Entity<Class>()
            .HasQueryFilter(c => !c.IsDisabled && c.OwnerId  == _userContext.UserId )
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Student>()
            .HasQueryFilter(s => !s.IsDisabled && s.OwnerId  == _userContext.UserId)
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Exam>()
            .HasQueryFilter(e => e.OwnerId  == _userContext.UserId)
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<StudentExamPaper>()
            .HasQueryFilter(e => e.OwnerId  == _userContext.UserId)
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ExamPage>()
            .HasQueryFilter(p => p.Exam.OwnerId  == _userContext.UserId);

        builder.Entity<StudentExamPage>()
            .HasQueryFilter(p => p.StudentExamPaper.OwnerId  == _userContext.UserId);

        base.OnModelCreating(builder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries()
                     .Where(e => e.State == EntityState.Added))
        {
            if (entry.Entity is Class c && string.IsNullOrEmpty(c.OwnerId))
                c.OwnerId = _userContext.UserId!;

            if (entry.Entity is Student s && string.IsNullOrEmpty(s.OwnerId))
                s.OwnerId = _userContext.UserId!;

            if (entry.Entity is Exam e && string.IsNullOrEmpty(e.OwnerId))
                e.OwnerId = _userContext.UserId!;

            if (entry.Entity is StudentExamPaper p && string.IsNullOrEmpty(p.OwnerId))
                p.OwnerId = _userContext.UserId!;
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}