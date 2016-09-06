namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reactive.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.CircuitBreakers;
    using NServiceBus.Features;
    using NServiceBus.Logging;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.Extensions;

    public class EventDispatcher : FeatureStartupTask
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(EventDispatcher));
        private readonly IBus bus;
        private readonly CriticalError criticalError;
        private readonly IEnumerable<IEventPublisher> eventPublishers;
        private readonly Settings settings;
        private readonly ManualResetEventSlim signal = new ManualResetEventSlim();
        private readonly IDocumentStore store;
        private RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;
        private IDisposable subscription;
        private Task task;
        private CancellationTokenSource tokenSource;

        public EventDispatcher(IDocumentStore store, IBus bus, CriticalError criticalError, Settings settings, IEnumerable<IEventPublisher> eventPublishers)
        {
            this.store = store;
            this.bus = bus;
            this.criticalError = criticalError;
            this.settings = settings;
            this.eventPublishers = eventPublishers;
        }

        protected override void OnStart()
        {
            subscription = store.Changes().ForDocumentsInCollection("ExternalIntegrationDispatchRequests").Where(c => c.Type == DocumentChangeTypes.Put).Subscribe(OnNext);

            tokenSource = new CancellationTokenSource();
            circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker("EventDispatcher",
                TimeSpan.FromMinutes(5),
                ex => criticalError.Raise("Repeated failures when dispatching external integration events.", ex),
                TimeSpan.FromSeconds(20));

            StartDispatcher();
        }

        protected override void OnStop()
        {
            subscription.Dispose();
            tokenSource.Cancel();
            tokenSource.Dispose();
            task?.Wait();
            task?.Dispose();
        }

        private void OnNext(DocumentChangeNotification documentChangeNotification)
        {
            signal.Set();
        }

        private void StartDispatcher()
        {
            task = StartDispatcherTask();
        }

        async Task StartDispatcherTask()
        {
            try
            {
                do
                {
                    try
                    {
                        await signal.WaitHandle.WaitOneAsync(tokenSource.Token);
                        signal.Reset();
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    DispatchEvents(tokenSource.Token);
                } while (!tokenSource.IsCancellationRequested);
            }
            catch (Exception ex)
            {
                Logger.Error("An exception occurred when dispatching external integration events", ex);
                circuitBreaker.Failure(ex);

                if (!tokenSource.IsCancellationRequested)
                {
                    StartDispatcher();
                }
            }
        }

        private void DispatchEvents(CancellationToken token)
        {
            bool more;

            do
            {
                more = TryDispatchEventBatch();

                circuitBreaker.Success();

                if (more)
                {
                    //if there is more events to dispatch we sleep for a bit and then we go again
                    token.WaitHandle.WaitOne(TimeSpan.FromSeconds(1));
                }
            } while (!token.IsCancellationRequested && more);
        }

        private bool TryDispatchEventBatch()
        {
            using (var session = store.OpenSession())
            {
                var awaitingDispatching = session.Query<ExternalIntegrationDispatchRequest>().Take(settings.ExternalIntegrationsDispatchingBatchSize);
                if (!awaitingDispatching.Any())
                {
                    return false;
                }

                var allContexts = awaitingDispatching.Select(r => r.DispatchContext).ToArray();
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Dispatching {allContexts.Length} events.");
                }
                var eventsToBePublished = eventPublishers.SelectMany(p => p.PublishEventsForOwnContexts(allContexts, session));

                foreach (var eventToBePublished in eventsToBePublished)
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.Debug("Publishing external event on the bus.");
                    }

                    try
                    {
                        bus.Publish(eventToBePublished);
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
                        bus.Publish(m);
                    }
                }
                foreach (var dispatchedEvent in awaitingDispatching)
                {
                    session.Delete(dispatchedEvent);
                }

                session.SaveChanges();
            }

            return true;
        }
    }
}