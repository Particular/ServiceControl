namespace ServiceControl.Persistence.Sql.MySQL;

using Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

class MySqlDatabaseMigrator : IDatabaseMigrator
{
    readonly IServiceProvider serviceProvider;
    readonly ILogger<MySqlDatabaseMigrator> logger;

    public MySqlDatabaseMigrator(
        IServiceProvider serviceProvider,
        ILogger<MySqlDatabaseMigrator> logger)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
    }

    public async Task ApplyMigrations(CancellationToken cancellationToken)
    {
        logger.LogInformation("Initializing MySQL database");

        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MySqlDbContext>();

            logger.LogDebug("Testing database connectivity");
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                throw new Exception("Cannot connect to MySQL database. Check connection string and ensure database server is accessible.");
            }

            logger.LogInformation("Applying pending migrations");
            await dbContext.Database.MigrateAsync(cancellationToken);

            logger.LogInformation("MySQL database initialized successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize MySQL database");
            throw;
        }
    }
}
