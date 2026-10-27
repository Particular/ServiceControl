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

        await dbContext.RequireNoPrimaryMigrationsHistory(cancellationToken);

        logger.LogInformation("Starting audit database migration");
        await dbContext.Database.MigrateAsync(cancellationToken);

        var hour = AuditHours.Truncate(timeProvider.GetUtcNow().UtcDateTime);
        await partitions.EnsurePartitions(dbContext, hour.AddHours(-1), hour + AuditHours.Lookahead, cancellationToken);
        logger.LogInformation("Audit database migration completed");
    }

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
