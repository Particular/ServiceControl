namespace ServiceControl.Audit.Persistence.Sql.SqlServer;

using Core.Abstractions;
using Core.DbContexts;
using Core.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

class SqlServerAuditDatabaseMigrator(
    AuditDbContextBase dbContext,
    IPartitionManager partitionManager,
    TimeProvider timeProvider,
    ILogger<SqlServerAuditDatabaseMigrator> logger)
    : IAuditDatabaseMigrator
{
    public async Task ApplyMigrations(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting SQL Server database migration for Audit");

        await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

        // Ensure partitions exist before ingestion starts.
        // This handles gaps from downtime — creates partitions for today + 3 days ahead.
        var today = timeProvider.GetUtcNow().UtcDateTime.Date;
        await partitionManager.EnsurePartitionsExist(dbContext, today, daysAhead: 3, cancellationToken);

        logger.LogInformation("SQL Server database migration completed for Audit");
    }
}
