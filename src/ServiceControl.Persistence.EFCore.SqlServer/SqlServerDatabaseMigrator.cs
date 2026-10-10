namespace ServiceControl.Persistence.EFCore.SqlServer;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.DbContexts;

class SqlServerDatabaseMigrator(ServiceControlDbContext dbContext, ILogger<SqlServerDatabaseMigrator> logger) : IDatabaseMigrator
{
    public async Task ApplyMigrations(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting SQL Server database migration");

        var previousTimeout = dbContext.Database.GetCommandTimeout();
        dbContext.Database.SetCommandTimeout(EFPersisterSettings.MigrationCommandTimeout);

        await RequireSchema(cancellationToken);
        await dbContext.Database.MigrateAsync(cancellationToken);

        dbContext.Database.SetCommandTimeout(previousTimeout);

        logger.LogInformation("SQL Server database migration completed");
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
            .SqlQueryRaw<int>("SELECT CASE WHEN SCHEMA_ID({0}) IS NULL THEN 0 ELSE 1 END AS [Value]", dbContext.Schema)
            .SingleAsync(cancellationToken);

        if (exists == 0)
        {
            throw new InvalidOperationException(
                $"The configured schema '{dbContext.Schema}' does not exist in the database. ServiceControl does not create schemas, the same way it does not create the database. Create the schema, grant the configured user rights on it, and run setup again.");
        }
    }
}
