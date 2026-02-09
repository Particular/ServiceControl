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
    public async Task ApplyMigrations(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting PostgreSQL database migration for Audit");

        var originalTimeout = dbContext.Database.GetCommandTimeout();
        dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(15));

        try
        {
            await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            dbContext.Database.SetCommandTimeout(originalTimeout);
        }

        logger.LogInformation("PostgreSQL database migration completed for Audit");
    }
}
