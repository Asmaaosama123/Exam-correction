
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamCorrection.Persistance.EntitiesConfigurations;

public class RoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
	public void Configure(EntityTypeBuilder<ApplicationRole> builder)
	{
		//Default Data
		builder.HasData([
			new ApplicationRole
			{
				Id = DefaultRoles.AdminRoleId,
				Name = DefaultRoles.Admin,
				NormalizedName = DefaultRoles.Admin.ToUpper(),
				ConcurrencyStamp = DefaultRoles.AdminRoleConcurrencyStamp,
			},
			new ApplicationRole
			{
				Id = DefaultRoles.MemberRoleId,
				Name = DefaultRoles.Teacher,
				NormalizedName = DefaultRoles.Teacher.ToUpper(),
				ConcurrencyStamp = DefaultRoles.MemberRoleConcurrencyStamp,
				IsDefault = true,
			}
		]);
	}
}