namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus;
using ServiceControl.ExternalIntegrations;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.Entities;

public class ExternalIntegrationRequestsDataStore(
    IServiceScopeFactory scopeFactory,
    EFPersisterSettings settings,
    TimeProvider timeProvider,
    CriticalError criticalError,
    ILogger<ExternalIntegrationRequestsDataStore> logger)
    : DataStoreBase(scopeFactory), IExternalIntegrationRequestsDataStore, IHostedService, IAsyncDisposable
{
    static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(30);

    Func<object[], CancellationToken, Task>? callback;
    Task? task;
    bool isDisposed;

    public async Task StoreDispatchRequest(IEnumerable<ExternalIntegrationDispatchRequest> dispatchRequests, CancellationToken cancellationToken = default)
    {
        await ExecuteWithDbContext(async (context, cancellationToken) =>
        {
            foreach (var dispatchRequest in dispatchRequests)
            {
                var (typeName, json) = DispatchContextSerializer.Serialize(dispatchRequest.DispatchContext);

                context.ExternalIntegrationDispatchRequests.Add(new ExternalIntegrationDispatchRequestEntity
                {
                    DispatchContextTypeName = typeName,
                    DispatchContextJson = json
                });
            }

            await context.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        signal.Release();
    }

    public void Subscribe(Func<object[], CancellationToken, Task> callback)
    {
        if (this.callback is not null)
        {
            throw new InvalidOperationException("Subscription already exists.");
        }

        this.callback = callback ?? throw new ArgumentNullException(nameof(callback));
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        task = RunLoop(tokenSource.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default) => await DisposeAsync();

    public Task DispatchNow(CancellationToken cancellationToken = default) => DrainAvailableBatches(cancellationToken);

    async Task RunLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await DrainAvailableBatches(cancellationToken);
                circuitBreaker.Success();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An exception occurred when dispatching external integration events");

                await circuitBreaker.Failure(ex, cancellationToken);
                continue;
            }

            try
            {
                await Task.WhenAny(signal.WaitAsync(cancellationToken), Task.Delay(PollingInterval, timeProvider, cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    async Task DrainAvailableBatches(CancellationToken cancellationToken)
    {
        var subscribedCallback = callback;

        if (subscribedCallback is null)
        {
            return;
        }

        await drainLock.WaitAsync(cancellationToken);

        try
        {
            while (await TryDispatchBatch(subscribedCallback, cancellationToken) && !cancellationToken.IsCancellationRequested)
            {
            }
        }
        finally
        {
            drainLock.Release();
        }
    }

    async Task<bool> TryDispatchBatch(Func<object[], CancellationToken, Task> callback, CancellationToken cancellationToken)
    {
        var rows = await ExecuteWithDbContext((context, cancellationToken) =>
            context.ExternalIntegrationDispatchRequests
                .AsNoTracking()
                .OrderBy(row => row.Id)
                .Take(settings.ExternalIntegrationsDispatchingBatchSize)
                .ToListAsync(cancellationToken), cancellationToken);

        if (rows.Count == 0)
        {
            return false;
        }

        var contexts = rows
            .Select(row => DispatchContextSerializer.Deserialize(row.DispatchContextTypeName, row.DispatchContextJson))
            .ToArray();

        logger.LogDebug("Dispatching {EventCount} events", contexts.Length);

        // Rows are only deleted once the callback has successfully consumed the batch: if it
        // throws (e.g. a transient failure resolving one of the outgoing contract events), the
        // rows are left in place so the same batch is retried on the next pass.
        await callback(contexts, cancellationToken);

        var ids = rows.Select(row => row.Id).ToArray();

        await ExecuteWithDbContext(
            (context, cancellationToken) => context.ExternalIntegrationDispatchRequests
                .Where(row => ids.Contains(row.Id))
                .ExecuteDeleteAsync(cancellationToken),
            cancellationToken);

        return rows.Count == settings.ExternalIntegrationsDispatchingBatchSize;
    }

    public async ValueTask DisposeAsync()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        await tokenSource.CancelAsync();

        if (task is not null)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                // expected during shutdown
            }
        }

        tokenSource.Dispose();
        signal.Dispose();
        drainLock.Dispose();
    }

    readonly SemaphoreSlim signal = new(0, int.MaxValue);
    readonly SemaphoreSlim drainLock = new(1, 1);
    readonly CancellationTokenSource tokenSource = new();

    readonly RepeatedFailuresOverTimeCircuitBreaker circuitBreaker = new(
        "ExternalIntegrationEventDispatcher",
        TimeSpan.FromMinutes(5),
        ex => criticalError.Raise("Repeated failures when dispatching external integration events.", ex),
        logger,
        timeToWaitWhenArmed: TimeSpan.FromSeconds(20));
}
