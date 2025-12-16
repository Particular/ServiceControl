namespace ServiceControl.Persistence.Sql.Core.Implementation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Entities;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServiceControl.ExternalIntegrations;
using ServiceControl.Persistence;

public class ExternalIntegrationRequestsDataStore : DataStoreBase, IExternalIntegrationRequestsDataStore, IAsyncDisposable
{
    readonly ILogger<ExternalIntegrationRequestsDataStore> logger;
    readonly CancellationTokenSource tokenSource = new();

    Func<object[], Task>? callback;
    Task? dispatcherTask;
    bool isDisposed;

    public ExternalIntegrationRequestsDataStore(
        IServiceProvider serviceProvider,
        ILogger<ExternalIntegrationRequestsDataStore> logger) : base(serviceProvider)
    {
        this.logger = logger;
    }

    public Task StoreDispatchRequest(IEnumerable<ExternalIntegrationDispatchRequest> dispatchRequests)
    {
        return ExecuteWithDbContext(async dbContext =>
        {
            foreach (var dispatchRequest in dispatchRequests)
            {
                if (dispatchRequest.Id != null)
                {
                    throw new ArgumentException("Items cannot have their Id property set");
                }

                var entity = new ExternalIntegrationDispatchRequestEntity
                {
                    DispatchContextJson = JsonSerializer.Serialize(dispatchRequest.DispatchContext, JsonSerializationOptions.Default),
                    CreatedAt = DateTime.UtcNow
                };

                await dbContext.ExternalIntegrationDispatchRequests.AddAsync(entity);
            }

            await dbContext.SaveChangesAsync();
        });
    }

    public void Subscribe(Func<object[], Task> callback)
    {
        if (this.callback != null)
        {
            throw new InvalidOperationException("Subscription already exists.");
        }

        this.callback = callback ?? throw new ArgumentNullException(nameof(callback));

        // Start the dispatcher task if not already running
        dispatcherTask ??= DispatcherLoop(tokenSource.Token);
    }

    async Task DispatcherLoop(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await DispatchBatch(cancellationToken);

                    // Wait before checking for more events
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error dispatching external integration events");
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during shutdown
        }
    }

    async Task DispatchBatch(CancellationToken cancellationToken)
    {
        await ExecuteWithDbContext(async dbContext =>
        {
            var batchSize = 100; // Default batch size
            var requests = await dbContext.ExternalIntegrationDispatchRequests
                .OrderBy(r => r.CreatedAt)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (requests.Count == 0)
            {
                return;
            }

            var contexts = requests
                .Select(r => JsonSerializer.Deserialize<object>(r.DispatchContextJson, JsonSerializationOptions.Default)!)
                .ToArray();

            logger.LogDebug("Dispatching {EventCount} events", contexts.Length);

            if (callback != null)
            {
                await callback(contexts);
            }

            // Remove dispatched requests
            dbContext.ExternalIntegrationDispatchRequests.RemoveRange(requests);
            await dbContext.SaveChangesAsync(cancellationToken);
        });
    }

    public async Task StopAsync(CancellationToken cancellationToken) => await DisposeAsync();

    public async ValueTask DisposeAsync()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        await tokenSource.CancelAsync();

        if (dispatcherTask != null)
        {
            await dispatcherTask;
        }

        tokenSource?.Dispose();
    }
}
