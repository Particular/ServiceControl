namespace ServiceControl.Persistence.Sql.PostgreSQL;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

// Design-time factory for EF Core tools (migrations, etc.)
class PostgreSqlDbContextFactory : IDesignTimeDbContextFactory<PostgreSqlDbContext>
{
    public PostgreSqlDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PostgreSqlDbContext>();

        // Use a dummy connection string for design-time operations
        optionsBuilder.UseNpgsql("Host=localhost;Database=servicecontrol;Username=postgres;Password=postgres");

        return new PostgreSqlDbContext(optionsBuilder.Options);
    }
}
