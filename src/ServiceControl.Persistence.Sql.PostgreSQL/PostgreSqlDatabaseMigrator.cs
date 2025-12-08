namespace ServiceControl.Persistence.Sql.PostgreSQL;

using Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

class PostgreSqlDatabaseMigrator : IDatabaseMigrator
{
    readonly IServiceProvider serviceProvider;
    readonly ILogger<PostgreSqlDatabaseMigrator> logger;

    public PostgreSqlDatabaseMigrator(
        IServiceProvider serviceProvider,
        ILogger<PostgreSqlDatabaseMigrator> logger)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
    }

    public async Task ApplyMigrations(CancellationToken cancellationToken)
    {
        logger.LogInformation("Initializing PostgreSQL database");

        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PostgreSqlDbContext>();

            logger.LogDebug("Testing database connectivity");
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                throw new Exception("Cannot connect to PostgreSQL database. Check connection string and ensure database server is accessible.");
            }

            logger.LogInformation("Applying pending migrations");
            await dbContext.Database.MigrateAsync(cancellationToken);

            logger.LogInformation("PostgreSQL database initialized successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize PostgreSQL database");
            throw;
        }
    }
}
