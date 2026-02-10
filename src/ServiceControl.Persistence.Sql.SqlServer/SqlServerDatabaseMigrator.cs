namespace ServiceControl.Persistence.Sql.SqlServer;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceControl.Persistence;

class SqlServerDatabaseMigrator : IDatabaseMigrator
{
    readonly IServiceProvider serviceProvider;
    readonly ILogger<SqlServerDatabaseMigrator> logger;

    public SqlServerDatabaseMigrator(
        IServiceProvider serviceProvider,
        ILogger<SqlServerDatabaseMigrator> logger)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
    }

    public async Task ApplyMigrations(CancellationToken cancellationToken)
    {
        logger.LogInformation("Initializing SQL Server database");

        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SqlServerDbContext>();

            logger.LogDebug("Testing database connectivity");
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                throw new Exception("Cannot connect to SQL Server database. Check connection string and ensure database server is accessible.");
            }

            logger.LogInformation("Applying pending migrations");
            await dbContext.Database.MigrateAsync(cancellationToken);

            logger.LogInformation("SQL Server database initialized successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize SQL Server database");
            throw;
        }
    }
}
