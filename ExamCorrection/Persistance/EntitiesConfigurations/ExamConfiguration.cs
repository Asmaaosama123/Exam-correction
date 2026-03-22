namespace ExamCorrection.Persistance.EntitiesConfigurations;

public class ExamConfiguration : IEntityTypeConfiguration<Exam>
{
    public void Configure(EntityTypeBuilder<Exam> builder)
    {
        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(s => new { s.Title, s.OwnerId }).IsUnique();

        builder.Property(x => x.Subject)
            .IsRequired()
            .HasMaxLength(200);
    }
}