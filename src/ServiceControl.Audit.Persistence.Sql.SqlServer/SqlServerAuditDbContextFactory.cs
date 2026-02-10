namespace ServiceControl.Audit.Persistence.Sql.SqlServer;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

/// <summary>
/// Design-time factory for EF Core migrations tooling.
/// </summary>
public class SqlServerAuditDbContextFactory : IDesignTimeDbContextFactory<SqlServerAuditDbContext>
{
    public SqlServerAuditDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SqlServerAuditDbContext>();

        // Use a default connection string for design-time operations
        // This is only used when running EF Core migrations tooling
        var connectionString = Environment.GetEnvironmentVariable("SERVICECONTROL_AUDIT_DATABASE_CONNECTIONSTRING")
            ?? "Server=localhost;Database=ServiceControlAudit;Trusted_Connection=True;TrustServerCertificate=True;";

        optionsBuilder.UseSqlServer(connectionString);

        return new SqlServerAuditDbContext(optionsBuilder.Options);
    }
}
