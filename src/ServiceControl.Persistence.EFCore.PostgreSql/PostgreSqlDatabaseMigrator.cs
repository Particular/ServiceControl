namespace ServiceControl.Persistence.EFCore.PostgreSql;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Implementation.Audit;

class PostgreSqlDatabaseMigrator(
    ServiceControlDbContext dbContext,
    IAuditPartitionManager auditPartitions,
    TimeProvider timeProvider,
    ILogger<PostgreSqlDatabaseMigrator> logger) : IDatabaseMigrator
{
    public async Task ApplyMigrations(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting PostgreSQL database migration");

        var previousTimeout = dbContext.Database.GetCommandTimeout();
        dbContext.Database.SetCommandTimeout(EFPersisterSettings.MigrationCommandTimeout);

        await RequireSchema(cancellationToken);
        await dbContext.Database.MigrateAsync(cancellationToken);
        await ProvisionAuditPartitions(cancellationToken);

        dbContext.Database.SetCommandTimeout(previousTimeout);

        logger.LogInformation("PostgreSQL database migration completed");
    }

    // A fresh instance has to ingest before its first retention sweep provisions anything, and the
    // hour before now is included because a batch started just before the hour rolled still lands
    // in it.
    Task ProvisionAuditPartitions(CancellationToken cancellationToken)
    {
        var now = AuditHours.Truncate(timeProvider.GetUtcNow().UtcDateTime);

        return auditPartitions.EnsurePartitions(dbContext, now.AddHours(-1), now + AuditHours.Lookahead, cancellationToken);
    }

    // EF Core would create the schema on its way to creating the migrations history table, which
    // would turn a misspelled Database/Schema into a silently empty instance rather than an error.
    async Task RequireSchema(CancellationToken cancellationToken)
    {
        if (dbContext.Schema is null)
        {
            return;
        }

        var exists = await dbContext.Database
            .SqlQueryRaw<int>("""SELECT CASE WHEN EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = {0}) THEN 1 ELSE 0 END AS "Value" """, dbContext.Schema)
            .SingleAsync(cancellationToken);

        if (exists == 0)
        {
            throw new InvalidOperationException(
                $"The configured schema '{dbContext.Schema}' does not exist in the database. ServiceControl does not create schemas, the same way it does not create the database. Create the schema, grant the configured user rights on it, and run setup again.");
        }
    }
}
