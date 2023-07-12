﻿namespace ServiceControl.Persistence.RavenDb
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reactive.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using ExternalIntegrations;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceBus.Management.Infrastructure.Extensions;
    using static Lucene.Net.Documents.Field;

    public class ExternalIntegrationRequestsDataStore : IExternalIntegrationRequestsDataStore
    {
        public ExternalIntegrationRequestsDataStore(PersistenceSettings settings, IDocumentStore documentStore, CriticalError criticalError)
        {
            this.settings = settings;
            this.documentStore = documentStore;
            this.criticalError = criticalError;
        }

        public void Subscribe(Func<object[], Task> callback)
        {
            if (subscription != null)
            {
                throw new InvalidOperationException("Subscription already exists.");
            }

            this.callback = callback ?? throw new ArgumentNullException(nameof(callback));

            subscription = documentStore
                .Changes()
                .ForDocumentsStartingWith("ExternalIntegrationDispatchRequests")
                .Where(c => c.Type == DocumentChangeTypes.Put)
                .Subscribe(d =>
                {
                    latestEtag = Etag.Max(d.Etag, latestEtag);
                    signal.Set();
                });

            tokenSource = new CancellationTokenSource();
            circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("EventDispatcher",
                TimeSpan.FromMinutes(5),
                ex => criticalError.Raise("Repeated failures when dispatching external integration events.", ex),
                TimeSpan.FromSeconds(20));

            StartDispatcher();
        }

        void StartDispatcher()
        {
            task = StartDispatcherTask();
        }

        async Task StartDispatcherTask()
        {
            try
            {
                await DispatchEvents(tokenSource.Token).ConfigureAwait(false);
                do
                {
                    try
                    {
                        await signal.WaitHandle.WaitOneAsync(tokenSource.Token).ConfigureAwait(false);
                        signal.Reset();
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    await DispatchEvents(tokenSource.Token).ConfigureAwait(false);
                }
                while (!tokenSource.IsCancellationRequested);
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                Logger.Error("An exception occurred when dispatching external integration events", ex);
                await circuitBreaker.Failure(ex).ConfigureAwait(false);

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
                more = await TryDispatchEventBatch()
                    .ConfigureAwait(false);

                circuitBreaker.Success();

                if (more && !cancellationToken.IsCancellationRequested)
                {
                    //if there is more events to dispatch we sleep for a bit and then we go again
                    await Task.Delay(1000, CancellationToken.None).ConfigureAwait(false);
                }
            }
            while (!cancellationToken.IsCancellationRequested && more);
        }

        async Task<bool> TryDispatchEventBatch()
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var awaitingDispatching = await session
                    .Query<ExternalIntegrationDispatchRequest>()
                    .Statistics(out var stats)
                    .Take(settings.ExternalIntegrationsDispatchingBatchSize())
                    .ToListAsync()
                    .ConfigureAwait(false);

                if (awaitingDispatching.Count == 0)
                {
                    // If the index hasn't caught up, try again
                    return stats.IndexEtag.CompareTo(latestEtag) < 0;
                }

                var allContexts = awaitingDispatching.Select(r => r.DispatchContext).ToArray();
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Dispatching {allContexts.Length} events.");
                }

                await callback(allContexts).ConfigureAwait(false);

                foreach (var dispatchedEvent in awaitingDispatching)
                {
                    session.Delete(dispatchedEvent);
                }

                await session.SaveChangesAsync()
                    .ConfigureAwait(false);
            }

            return true;
        }

        public async Task Stop()
        {
            subscription.Dispose();
            tokenSource.Cancel();

            if (task != null)
            {
                await task.ConfigureAwait(false);
            }

            tokenSource.Dispose();
            circuitBreaker.Dispose();
        }

        readonly PersistenceSettings settings;
        readonly IDocumentStore documentStore;
        IDisposable subscription;
        Etag latestEtag = Etag.Empty;
        RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;
        Task task;
        ManualResetEventSlim signal = new ManualResetEventSlim();
        CancellationTokenSource tokenSource;
        CriticalError criticalError;
        Func<object[], Task> callback;

        static ILog Logger = LogManager.GetLogger(typeof(ExternalIntegrationRequestsDataStore));
    }
}