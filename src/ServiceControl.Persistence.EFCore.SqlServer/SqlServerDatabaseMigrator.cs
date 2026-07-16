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

        await dbContext.Database.MigrateAsync(cancellationToken);

        dbContext.Database.SetCommandTimeout(previousTimeout);

        logger.LogInformation("SQL Server database migration completed");
    }
}
