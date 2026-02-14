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
    public async Task ApplyMigrations(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting SQL Server database migration for Audit");

        var previousTimeout = dbContext.Database.GetCommandTimeout();
        dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(40));

        await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

        dbContext.Database.SetCommandTimeout(previousTimeout);

        logger.LogInformation("SQL Server database migration completed for Audit");
    }
}
