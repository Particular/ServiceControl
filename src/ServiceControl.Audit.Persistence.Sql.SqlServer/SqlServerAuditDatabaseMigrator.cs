namespace ServiceControl.Audit.Persistence.Sql.SqlServer;

using Core.Abstractions;
using Core.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

class SqlServerAuditDatabaseMigrator(
    AuditDbContextBase dbContext,
    ILogger<SqlServerAuditDatabaseMigrator> logger)
    : IAuditDatabaseMigrator
{
    public async Task Migrate(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting SQL Server database migration for Audit");

        await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("SQL Server database migration completed for Audit");
    }
}
