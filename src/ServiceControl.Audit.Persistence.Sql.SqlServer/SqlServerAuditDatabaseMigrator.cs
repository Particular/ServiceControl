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

        var originalTimeout = dbContext.Database.GetCommandTimeout();
        dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));

        try
        {
            await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            dbContext.Database.SetCommandTimeout(originalTimeout);
        }

        logger.LogInformation("SQL Server database migration completed for Audit");
    }
}
