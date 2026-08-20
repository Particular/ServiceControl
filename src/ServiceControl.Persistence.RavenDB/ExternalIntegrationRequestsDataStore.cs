namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reactive.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using ExternalIntegrations;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Changes;
    using ServiceControl.Infrastructure;

    class ExternalIntegrationRequestsDataStore : IExternalIntegrationRequestsDataStore, IHostedService, IAsyncDisposable
    {
        public ExternalIntegrationRequestsDataStore(
            RavenPersisterSettings settings,
            IRavenSessionProvider sessionProvider,
            IRavenDocumentStoreProvider documentStoreProvider,
            CriticalError criticalError,
            ILogger<ExternalIntegrationRequestsDataStore> logger)
        {
            this.settings = settings;
            this.sessionProvider = sessionProvider;
            this.documentStoreProvider = documentStoreProvider;
            this.logger = logger;
            var timeToWait = TimeSpan.FromMinutes(5);
            var delayAfterFailure = TimeSpan.FromSeconds(20);

            circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker(
                "EventDispatcher",
                timeToWait,
                ex => criticalError.Raise("Repeated failures when dispatching external integration events.", ex),
                logger,
                timeToWaitWhenArmed: delayAfterFailure
            );
        }

        const string KeyPrefix = "ExternalIntegrationDispatchRequests";

        public async Task StoreDispatchRequest(IEnumerable<ExternalIntegrationDispatchRequest> dispatchRequests, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            foreach (var dispatchRequest in dispatchRequests)
            {
                var id = KeyPrefix + "/" + Guid.NewGuid();
                await session.StoreAsync(dispatchRequest, id, cancellationToken);
            }

            await session.SaveChangesAsync(cancellationToken);
        }

        public void Subscribe(Func<object[], CancellationToken, Task> callback)
        {
            if (this.callback != null)
            {
                throw new InvalidOperationException("Subscription already exists.");
            }

            this.callback = callback ?? throw new ArgumentNullException(nameof(callback));

            StartDispatcher();
        }

        void StartDispatcher() => task = StartDispatcherTask(tokenSource.Token);

        async Task StartDispatcherTask(CancellationToken cancellationToken)
        {
            try
            {
                await DispatchEvents(cancellationToken);
                do
                {
                    try
                    {
                        await signal.WaitHandle.WaitOneAsync(cancellationToken);
                        signal.Reset();
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    await DispatchEvents(cancellationToken);
                }
                while (!cancellationToken.IsCancellationRequested);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // ignore
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An exception occurred when dispatching external integration events");

                try
                {
                    await circuitBreaker.Failure(ex, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Shutting down while backing off after a failure - nothing more to do.
                    return;
                }

                if (!tokenSource.IsCancellationRequested)
                {
                    StartDispatcher();
                }
            }
        }

        async Task DispatchEvents(CancellationToken cancellationToken)
        {
            bool more;

            do
            {
                more = await TryDispatchEventBatch(cancellationToken);

                circuitBreaker.Success();

                if (more && !cancellationToken.IsCancellationRequested)
                {
                    //if there is more events to dispatch we sleep for a bit and then we go again
                    await Task.Delay(1000, CancellationToken.None);
                }
            }
            while (!cancellationToken.IsCancellationRequested && more);
        }

        async Task<bool> TryDispatchEventBatch(CancellationToken cancellationToken)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var awaitingDispatching = await session
                .Query<ExternalIntegrationDispatchRequest>()
                .Statistics(out var stats)
                .Take(settings.ExternalIntegrationsDispatchingBatchSize)
                .ToListAsync(cancellationToken);

            if (awaitingDispatching.Count == 0)
            {
                // Should ensure we query again if the result is potentially stale
                // If ☝️ is not true we will need to use/parse the ChangeVector when document is written and compare to ResultEtag
                return stats.IsStale;
            }

            var allContexts = awaitingDispatching.Select(r => r.DispatchContext).ToArray();
            logger.LogDebug("Dispatching {EventCount} events", allContexts.Length);

            await callback(allContexts, cancellationToken);

            foreach (var dispatchedEvent in awaitingDispatching)
            {
                session.Delete(dispatchedEvent);
            }

            await session.SaveChangesAsync(cancellationToken);

            return true;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            var documentStore = await documentStoreProvider.GetDocumentStore(cancellationToken);
            subscription = documentStore
                .Changes()
                .ForDocumentsStartingWith(KeyPrefix)
                .Where(c => c.Type == DocumentChangeTypes.Put)
                .Subscribe(_ => signal.Set());
        }

        public async Task StopAsync(CancellationToken cancellationToken = default) => await DisposeAsync();

        public async ValueTask DisposeAsync()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            subscription?.Dispose();
            await tokenSource?.CancelAsync();

            if (task != null)
            {
                await task;
            }

            tokenSource?.Dispose();
            circuitBreaker.Dispose();
        }

        readonly RavenPersisterSettings settings;
        readonly IRavenSessionProvider sessionProvider;
        readonly IRavenDocumentStoreProvider documentStoreProvider;
        readonly CancellationTokenSource tokenSource = new();
        readonly RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;

        IDisposable subscription;
        Task task;
        readonly ManualResetEventSlim signal = new();
        Func<object[], CancellationToken, Task> callback;
        bool isDisposed;

        readonly ILogger<ExternalIntegrationRequestsDataStore> logger;
    }
}