namespace ServiceControl.Audit.Persistence.Sql.PostgreSQL;

using Core.Abstractions;
using Core.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

class PostgreSqlAuditDatabaseMigrator(
    AuditDbContextBase dbContext,
    ILogger<PostgreSqlAuditDatabaseMigrator> logger)
    : IAuditDatabaseMigrator
{
    public async Task Migrate(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting PostgreSQL database migration for Audit");

        await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("PostgreSQL database migration completed for Audit");
    }
}
