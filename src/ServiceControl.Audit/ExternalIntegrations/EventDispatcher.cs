﻿namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reactive.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Logging;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Settings;

    class EventDispatcher : FeatureStartupTask
    {
        public EventDispatcher(IDocumentStore store, IDomainEvents domainEvents, CriticalError criticalError, Settings settings, IEnumerable<IEventPublisher> eventPublishers)
        {
            this.store = store;
            this.criticalError = criticalError;
            this.settings = settings;
            this.eventPublishers = eventPublishers;
            this.domainEvents = domainEvents;
        }

        protected override Task OnStart(IMessageSession session)
        {
            subscription = store.Changes().ForDocumentsStartingWith("ExternalIntegrationDispatchRequests").Where(c => c.Type == DocumentChangeTypes.Put).Subscribe(OnNext);

            tokenSource = new CancellationTokenSource();
            circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("EventDispatcher",
                TimeSpan.FromMinutes(5),
                ex => criticalError.Raise("Repeated failures when dispatching external integration events.", ex),
                TimeSpan.FromSeconds(20));

            bus = session;

            StartDispatcher();
            return Task.FromResult(0);
        }

        void OnNext(DocumentChangeNotification documentChangeNotification)
        {
            latestEtag = Etag.Max(documentChangeNotification.Etag, latestEtag);
            signal.Set();
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
                } while (!tokenSource.IsCancellationRequested);
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

        async Task DispatchEvents(CancellationToken token)
        {
            bool more;

            do
            {
                more = await TryDispatchEventBatch()
                    .ConfigureAwait(false);

                circuitBreaker.Success();

                if (more && !token.IsCancellationRequested)
                {
                    //if there is more events to dispatch we sleep for a bit and then we go again
                    await Task.Delay(1000, CancellationToken.None).ConfigureAwait(false);
                }
            } while (!token.IsCancellationRequested && more);
        }

        async Task<bool> TryDispatchEventBatch()
        {
            using (var session = store.OpenAsyncSession())
            {
                var awaitingDispatching = await session
                    .Query<ExternalIntegrationDispatchRequest>()
                    .Statistics(out var stats)
                    .Take(settings.ExternalIntegrationsDispatchingBatchSize)
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

                var eventsToBePublished = new List<object>();
                foreach (var publisher in eventPublishers)
                {
                    var events = await publisher.PublishEventsForOwnContexts(allContexts, session)
                        .ConfigureAwait(false);
                    eventsToBePublished.AddRange(events);
                }

                foreach (var eventToBePublished in eventsToBePublished)
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.Debug("Publishing external event on the bus.");
                    }

                    try
                    {
                        await bus.Publish(eventToBePublished)
                            .ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Failed dispatching external integration event.", e);

                        var m = new ExternalIntegrationEventFailedToBePublished
                        {
                            EventType = eventToBePublished.GetType()
                        };
                        try
                        {
                            m.Reason = e.GetBaseException().Message;
                        }
                        catch (Exception)
                        {
                            m.Reason = "Failed to retrieve reason!";
                        }

                        await domainEvents.Raise(m)
                            .ConfigureAwait(false);
                    }
                }

                foreach (var dispatchedEvent in awaitingDispatching)
                {
                    session.Delete(dispatchedEvent);
                }

                await session.SaveChangesAsync()
                    .ConfigureAwait(false);
            }

            return true;
        }

        protected override async Task OnStop(IMessageSession session)
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

        IMessageSession bus;
        CriticalError criticalError;
        IEnumerable<IEventPublisher> eventPublishers;
        Settings settings;
        ManualResetEventSlim signal = new ManualResetEventSlim();
        IDocumentStore store;
        IDomainEvents domainEvents;
        RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;
        IDisposable subscription;
        Task task;
        CancellationTokenSource tokenSource;
        Etag latestEtag = Etag.Empty;
        static ILog Logger = LogManager.GetLogger(typeof(EventDispatcher));
    }
}