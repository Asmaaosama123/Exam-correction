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
    public DbSet<ExamGoal> ExamGoals { get; set; } = null!;
    public DbSet<Complaint> Complaints { get; set; } = null!;
    public DbSet<SystemErrorLog> SystemErrorLogs { get; set; } = null!;
    public DbSet<TutorialVideo> TutorialVideos { get; set; } = null!;
    public DbSet<SystemSetting> SystemSettings { get; set; } = null!;
    public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; } = null!;
    public DbSet<SubscriptionRequest> SubscriptionRequests { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        builder.Entity<SystemSetting>()
            .HasKey(s => s.Key);

        builder.Entity<SubscriptionRequest>()
            .HasOne(sr => sr.User)
            .WithMany()
            .HasForeignKey(sr => sr.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<SubscriptionRequest>()
            .HasOne(sr => sr.Plan)
            .WithMany()
            .HasForeignKey(sr => sr.PlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Class>()
            .HasQueryFilter(c => _userContext.IsAdmin || (!c.IsDisabled && c.OwnerId  == _userContext.UserId ))
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Student>()
            .HasQueryFilter(s => _userContext.IsAdmin || (!s.IsDisabled && s.OwnerId  == _userContext.UserId))
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Exam>()
            .HasQueryFilter(e => _userContext.IsAdmin || (e.OwnerId  == _userContext.UserId))
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<StudentExamPaper>()
            .HasQueryFilter(e => _userContext.IsAdmin || (e.OwnerId  == _userContext.UserId))
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ExamGoal>()
            .HasQueryFilter(e => _userContext.IsAdmin || (e.OwnerId == _userContext.UserId))
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Complaint>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<SystemErrorLog>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ExamPage>()
            .HasQueryFilter(p => _userContext.IsAdmin || (p.Exam.OwnerId  == _userContext.UserId));

        builder.Entity<StudentExamPage>()
            .HasQueryFilter(p => _userContext.IsAdmin || (p.StudentExamPaper.OwnerId  == _userContext.UserId));

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

            if (entry.Entity is ExamGoal g && string.IsNullOrEmpty(g.OwnerId))
                g.OwnerId = _userContext.UserId!;

            if (entry.Entity is Complaint cmp && string.IsNullOrEmpty(cmp.OwnerId))
                cmp.OwnerId = _userContext.UserId!;

            if (entry.Entity is SystemErrorLog log && string.IsNullOrEmpty(log.OwnerId))
                log.OwnerId = _userContext.UserId!;
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}