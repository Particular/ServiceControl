namespace ServiceControl.Audit.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceControl.Audit.Persistence.EFCore.Abstractions;
using ServiceControl.Audit.Persistence.EFCore.DbContexts;
using ServiceControl.Audit.Persistence.EFCore.Infrastructure;

sealed class DatabaseSetup(
    IServiceScopeFactory scopeFactory,
    IAuditPartitionManager partitions,
    TimeProvider timeProvider,
    ILogger<DatabaseSetup> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        dbContext.Database.SetCommandTimeout(EFPersisterSettings.MigrationCommandTimeout);

        if (dbContext.Schema is not null && !await dbContext.SchemaExists(dbContext.Schema, cancellationToken))
        {
            throw new InvalidOperationException(
                $"The configured schema '{dbContext.Schema}' does not exist in the database. ServiceControl does not create schemas, the same way it does not create the database. Create the schema, grant the configured user rights on it, and run setup again.");
        }

        logger.LogInformation("Starting audit database migration");
        await dbContext.Database.MigrateAsync(cancellationToken);

        var hour = AuditHours.Truncate(timeProvider.GetUtcNow().UtcDateTime);
        await partitions.EnsurePartitions(dbContext, hour.AddHours(-1), hour + AuditHours.Lookahead, cancellationToken);
        logger.LogInformation("Audit database migration completed");
    }

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
