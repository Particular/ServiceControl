namespace ServiceControl.Persistence.EFCore.PostgreSql;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.DbContexts;

class PostgreSqlDatabaseMigrator(ServiceControlDbContext dbContext, ILogger<PostgreSqlDatabaseMigrator> logger) : IDatabaseMigrator
{
    public async Task ApplyMigrations(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting PostgreSQL database migration");

        var previousTimeout = dbContext.Database.GetCommandTimeout();
        dbContext.Database.SetCommandTimeout(EFPersisterSettings.MigrationCommandTimeout);

        await dbContext.Database.MigrateAsync(cancellationToken);

        dbContext.Database.SetCommandTimeout(previousTimeout);

        logger.LogInformation("PostgreSQL database migration completed");
    }
}
