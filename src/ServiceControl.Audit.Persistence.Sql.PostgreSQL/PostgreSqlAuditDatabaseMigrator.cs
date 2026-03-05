namespace ServiceControl.Audit.Persistence.Sql.PostgreSQL;

using Core.Abstractions;
using Core.DbContexts;
using Core.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

class PostgreSqlAuditDatabaseMigrator(
    AuditDbContextBase dbContext,
    IPartitionManager partitionManager,
    TimeProvider timeProvider,
    ILogger<PostgreSqlAuditDatabaseMigrator> logger)
    : IAuditDatabaseMigrator
{
    public async Task ApplyMigrations(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting PostgreSQL database migration for Audit");

        await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

        // Ensure partitions exist before ingestion starts.
        // This handles gaps from downtime — creates partitions for now + 6 hours ahead.
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await partitionManager.EnsurePartitionsExist(dbContext, now, hoursAhead: 6, cancellationToken);

        logger.LogInformation("PostgreSQL database migration completed for Audit");
    }
}
