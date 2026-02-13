namespace ExamCorrection.Persistance.EntitiesConfigurations;

public class ClassConfiguration : IEntityTypeConfiguration<Class>
{
	public void Configure(EntityTypeBuilder<Class> builder)
	{
		builder.Property(x => x.Name).HasMaxLength(50);
		builder.HasIndex(s => new { s.Name, s.OwnerId }).IsUnique();
    }
}