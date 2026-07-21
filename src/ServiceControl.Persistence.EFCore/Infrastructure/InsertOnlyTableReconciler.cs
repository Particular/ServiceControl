namespace ServiceControl.Persistence.EFCore.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceControl.Persistence.EFCore.DbContexts;

public abstract class InsertOnlyTableReconciler(
    ILogger logger,
    TimeProvider timeProvider,
    IServiceScopeFactory serviceScopeFactory,
    string serviceName) : BackgroundService
{
    protected const int BatchSize = 1000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting {ServiceName}", serviceName);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), timeProvider, stoppingToken);

            using PeriodicTimer timer = new(TimeSpan.FromSeconds(30), timeProvider);

            do
            {
                try
                {
                    await Reconcile(pace: true, stoppingToken);
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

    // Drains all pending rows immediately, bypassing the background timer and the inter-batch pacing.
    // Intended for tests that need reconciled data visible without waiting for the timer loop.
    public Task ReconcileNow(CancellationToken cancellationToken = default) => Reconcile(pace: false, cancellationToken);

    async Task Reconcile(bool pace, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
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
            }, cancellationToken);

            if (rowsAffected < BatchSize)
            {
                break;
            }

            if (pace)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), timeProvider, cancellationToken);
            }
        }
    }

    protected abstract Task<int> ReconcileBatch(ServiceControlDbContext dbContext, CancellationToken stoppingToken);
}
