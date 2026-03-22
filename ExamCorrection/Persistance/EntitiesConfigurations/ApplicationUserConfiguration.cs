using ExamCorrection.Abstractions.Consts;
using ExamCorrection.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamCorrection.Persistance.EntitiesConfigurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
	public void Configure(EntityTypeBuilder<ApplicationUser> builder)
	{
		builder.Property(x => x.FirstName).HasMaxLength(100);
		builder.Property(x => x.LastName).HasMaxLength(100);
        builder
            .OwnsMany(x => x.RefreshTokens)
            .ToTable("RefreshTokens")
            .WithOwner()
            .HasForeignKey("UserId");

        var passwordHasher = new PasswordHasher<ApplicationUser>();

        //Default Data
        builder.HasData(new ApplicationUser
        {
            Id = DefaultUsers.AdminId,
            FirstName = DefaultUsers.AdminFirstName,
            LastName = DefaultUsers.AdminLastName,
            UserName = DefaultUsers.AdminEmail,
            NormalizedUserName = DefaultUsers.AdminEmail.ToUpper(),
            Email = DefaultUsers.AdminEmail,
            PhoneNumber = DefaultUsers.AdminPhoneNumber,
            NormalizedEmail = DefaultUsers.AdminEmail.ToUpper(),
            SecurityStamp = DefaultUsers.AdminSecurityStamp,
            ConcurrencyStamp = DefaultUsers.AdminConcurrencyStamp,
            EmailConfirmed = true,
            PasswordHash = DefaultUsers.AdminPasswordHash
        });
    }
}