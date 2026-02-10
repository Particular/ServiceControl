namespace ServiceControl.Audit.Persistence.Sql.PostgreSQL;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

/// <summary>
/// Design-time factory for EF Core migrations tooling.
/// </summary>
public class PostgreSqlAuditDbContextFactory : IDesignTimeDbContextFactory<PostgreSqlAuditDbContext>
{
    public PostgreSqlAuditDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PostgreSqlAuditDbContext>();

        // Use a default connection string for design-time operations
        // This is only used when running EF Core migrations tooling
        var connectionString = Environment.GetEnvironmentVariable("SERVICECONTROL_AUDIT_DATABASE_CONNECTIONSTRING")
            ?? "Host=localhost;Database=servicecontrol_audit;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString);

        return new PostgreSqlAuditDbContext(optionsBuilder.Options);
    }
}
