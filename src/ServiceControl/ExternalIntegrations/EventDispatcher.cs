namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;

    public class EventDispatcher : IWantToRunWhenBusStartsAndStops
    {
        public IDocumentStore DocumentStore { get; set; }
        public IBus Bus { get; set; }
        public IEnumerable<IEventPublisher> EventPublishers { get; set; }

        public void Start()
        {
            tokenSource = new CancellationTokenSource();
            Task.Factory.StartNew(() => DispatchEvents(tokenSource.Token));
        }

        public void Stop()
        {
            tokenSource.Cancel();
        }

        private void DispatchEvents(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                DispatchEventBatch(token);
            }
        }

        void DispatchEventBatch(CancellationToken token)
        {
            using (var session = DocumentStore.OpenSession())
            {
                var awaitingDispatching = session.Query<ExternalIntegrationDispatchRequest>().Take(Settings.ExternalIntegrationsDispatchingBatchSize).ToList();
                if (!awaitingDispatching.Any())
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.Debug("Nothing to dispatch. Waiting...");
                    }
                    token.WaitHandle.WaitOne(TimeSpan.FromSeconds(1));
                    return;
                }

                var allReferences = awaitingDispatching.Select(r => r.Reference).ToArray();
                if (Logger.IsDebugEnabled)
                {
                    Logger.DebugFormat("Dispatching {0} events.", allReferences.Length);
                }
                var eventsToBePublished = EventPublishers.SelectMany(p => p.PublishEventsForOwnReferences(allReferences, session));

                foreach (var evnt in eventsToBePublished)
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.DebugFormat("Publishing external event on the bus.");
                    }
                    Bus.Publish(evnt);
                }
                foreach (var dispatchedEvent in awaitingDispatching)
                {
                    session.Delete(dispatchedEvent);
                }
                session.SaveChanges();
            }
        }

        CancellationTokenSource tokenSource;

        static readonly ILog Logger = LogManager.GetLogger(typeof(EventDispatcher));
    }
}