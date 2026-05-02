using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace ExamCorrection.Persistance
{
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            var builder = new DbContextOptionsBuilder<ApplicationDbContext>();
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            builder.UseSqlServer(connectionString);

            // We provide a dummy IUserContext for design-time
            return new ApplicationDbContext(builder.Options, new DummyUserContext());
        }
    }

    public class DummyUserContext : IUserContext
    {
        public string? UserId => null;
        public string? UserEmail => null;
        public bool IsAdmin => false;
        public bool IsAuthenticated => false;
        public System.Collections.Generic.IEnumerable<string> Roles => new System.Collections.Generic.List<string>();
    }
}
