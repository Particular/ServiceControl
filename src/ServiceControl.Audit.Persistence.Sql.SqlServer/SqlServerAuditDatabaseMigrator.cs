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

        var previousTimeout = dbContext.Database.GetCommandTimeout();
        dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(40));

        await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

        dbContext.Database.SetCommandTimeout(previousTimeout);

        // Ensure partitions exist before ingestion starts.
        // This handles gaps from downtime — creates partitions for now + 6 hours ahead.
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await partitionManager.EnsurePartitionsExist(dbContext, now, hoursAhead: 6, cancellationToken);

        logger.LogInformation("SQL Server database migration completed for Audit");
    }
}
