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
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Changes;
    using ServiceControl.Infrastructure;

    class ExternalIntegrationRequestsDataStore
        : IExternalIntegrationRequestsDataStore
        , IHostedService
        , IAsyncDisposable
    {

        public ExternalIntegrationRequestsDataStore(RavenPersisterSettings settings, IRavenSessionProvider sessionProvider, IRavenDocumentStoreProvider documentStoreProvider, CriticalError criticalError)
        {
            this.settings = settings;
            this.sessionProvider = sessionProvider;
            this.documentStoreProvider = documentStoreProvider;

            var timeToWait = TimeSpan.FromMinutes(5);
            var delayAfterFailure = TimeSpan.FromSeconds(20);

            circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker(
                "EventDispatcher",
                timeToWait,
                ex => criticalError.Raise("Repeated failures when dispatching external integration events.", ex),
                delayAfterFailure
            );
        }

        const string KeyPrefix = "ExternalIntegrationDispatchRequests";

        public async Task StoreDispatchRequest(IEnumerable<ExternalIntegrationDispatchRequest> dispatchRequests)
        {
            using var session = await sessionProvider.OpenSession();
            foreach (var dispatchRequest in dispatchRequests)
            {
                if (dispatchRequest.Id != null)
                {
                    throw new ArgumentException("Items cannot have their Id property set");
                }

                dispatchRequest.Id = KeyPrefix + "/" + Guid.NewGuid();
                await session.StoreAsync(dispatchRequest);
            }

            await session.SaveChangesAsync();
        }

        public void Subscribe(Func<object[], Task> callback)
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
                Logger.Error("An exception occurred when dispatching external integration events", ex);
                await circuitBreaker.Failure(ex);

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
                more = await TryDispatchEventBatch();

                circuitBreaker.Success();

                if (more && !cancellationToken.IsCancellationRequested)
                {
                    //if there is more events to dispatch we sleep for a bit and then we go again
                    await Task.Delay(1000, CancellationToken.None);
                }
            }
            while (!cancellationToken.IsCancellationRequested && more);
        }

        async Task<bool> TryDispatchEventBatch()
        {
            using var session = await sessionProvider.OpenSession();
            var awaitingDispatching = await session
                .Query<ExternalIntegrationDispatchRequest>()
                .Statistics(out var stats)
                .Take(settings.ExternalIntegrationsDispatchingBatchSize)
                .ToListAsync();

            if (awaitingDispatching.Count == 0)
            {
                // Should ensure we query again if the result is potentially stale
                // If ☝️ is not true we will need to use/parse the ChangeVector when document is written and compare to ResultEtag
                return stats.IsStale;
            }

            var allContexts = awaitingDispatching.Select(r => r.DispatchContext).ToArray();
            if (Logger.IsDebugEnabled)
            {
                Logger.Debug($"Dispatching {allContexts.Length} events.");
            }

            await callback(allContexts);

            foreach (var dispatchedEvent in awaitingDispatching)
            {
                session.Delete(dispatchedEvent);
            }

            await session.SaveChangesAsync();

            return true;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var documentStore = await documentStoreProvider.GetDocumentStore(cancellationToken);
            subscription = documentStore
                .Changes()
                .ForDocumentsStartingWith(KeyPrefix)
                .Where(c => c.Type == DocumentChangeTypes.Put)
                .Subscribe(d =>
                {
                    signal.Set();
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
            subscription?.Dispose();
            await tokenSource?.CancelAsync();

            if (task != null)
            {
                await task;
            }

            tokenSource?.Dispose();
            circuitBreaker?.Dispose();
        }

        readonly RavenPersisterSettings settings;
        readonly IRavenSessionProvider sessionProvider;
        readonly IRavenDocumentStoreProvider documentStoreProvider;
        readonly CancellationTokenSource tokenSource = new();
        readonly RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;

        IDisposable subscription;
        Task task;
        ManualResetEventSlim signal = new();
        Func<object[], Task> callback;
        bool isDisposed;

        static ILog Logger = LogManager.GetLogger(typeof(ExternalIntegrationRequestsDataStore));
    }
}