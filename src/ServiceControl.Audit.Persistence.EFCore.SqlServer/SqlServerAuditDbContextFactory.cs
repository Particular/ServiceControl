namespace ServiceControl.Audit.Persistence.EFCore.SqlServer;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ServiceControl.Audit.Persistence.EFCore.DbContexts;

public class SqlServerAuditDbContextFactory : IDesignTimeDbContextFactory<SqlServerAuditDbContext>
{
    public SqlServerAuditDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SERVICECONTROL_DATABASE_CONNECTIONSTRING")
            ?? "Server=localhost;Database=ServiceControlAudit;Trusted_Connection=True;TrustServerCertificate=True";

        var optionsBuilder = new DbContextOptionsBuilder<SqlServerAuditDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sqlOptions => sqlOptions.MigrationsHistoryTable(AuditDbContext.MigrationsHistoryTableName));

        return new SqlServerAuditDbContext(optionsBuilder.Options);
    }
}
