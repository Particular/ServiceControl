namespace ServiceControl.Persistence.EFCore.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceControl.Persistence.EFCore.DbContexts;

public abstract class InsertOnlyTableReconciler<TInsertOnly, TTarget>(
    ILogger logger,
    TimeProvider timeProvider,
    IServiceScopeFactory serviceScopeFactory,
    string serviceName) : BackgroundService
    where TInsertOnly : class
    where TTarget : class
{
    protected const int BatchSize = 1000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting {ServiceName}", serviceName);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            using PeriodicTimer timer = new(TimeSpan.FromSeconds(30), timeProvider);

            do
            {
                try
                {
                    await Reconcile(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Error during {ServiceName} reconciliation", serviceName);
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Stopping {ServiceName}", serviceName);
        }
    }

    async Task Reconcile(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

            // A retrying execution strategy (EnableRetryOnFailure) rejects user-initiated transactions
            // unless the whole unit is wrapped so it can be retried as one.
            var strategy = dbContext.Database.CreateExecutionStrategy();
            var rowsAffected = await strategy.ExecuteAsync(dbContext, async (context, ct) =>
            {
                await using var transaction = await context.Database.BeginTransactionAsync(ct);
                var rows = await ReconcileBatch(context, ct);
                await transaction.CommitAsync(ct);
                return rows;
            }, stoppingToken);

            if (rowsAffected < BatchSize)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    protected abstract Task<int> ReconcileBatch(ServiceControlDbContext dbContext, CancellationToken stoppingToken);
}
