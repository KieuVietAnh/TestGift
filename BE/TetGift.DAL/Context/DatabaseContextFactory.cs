using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace TetGift.DAL.Context
{
    public class DatabaseContextFactory : IDesignTimeDbContextFactory<DatabaseContext>
    {
        public DatabaseContext CreateDbContext(string[] args)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory()) // chạy tại startup project
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddJsonFile("appsettings.Production.json", optional: true)
                .Build();

            var connStr = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                ?? config.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connStr))
                throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");

            var optionsBuilder = new DbContextOptionsBuilder<DatabaseContext>();
            optionsBuilder.UseNpgsql(connStr);

            return new DatabaseContext(optionsBuilder.Options);
        }
    }
}
