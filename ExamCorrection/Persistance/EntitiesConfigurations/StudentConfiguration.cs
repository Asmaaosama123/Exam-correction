namespace ExamCorrection.Persistance.EntitiesConfigurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
	public void Configure(EntityTypeBuilder<Student> builder)
	{
		builder.Property(x => x.FullName).HasMaxLength(150);
		builder.Property(x => x.Email).HasMaxLength(200);
		builder.Property(x => x.MobileNumber).HasMaxLength(15);

        builder.HasIndex(x => x.Email).IsUnique();
        builder.HasIndex(x => x.MobileNumber).IsUnique();
    }
}