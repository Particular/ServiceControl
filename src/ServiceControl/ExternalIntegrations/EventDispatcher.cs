namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceControl.Contracts.MessageFailures;

    public class EventDispatcher : IWantToRunWhenBusStartsAndStops
    {
        const int BatchSize = 10;
        public IDocumentStore DocumentStore { get; set; }
        public IBus Bus { get; set; }

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
                var awaitingDispatching = session.Query<StoredEvent, StoredEventsDispatchingIndex>()
                    .Where(x => !x.Dispatched)
                    .OrderBy(x => x.RegistrationDate)
                    .Take(BatchSize)
                    .ToList()
                    .Where(x => !x.Dispatched) //Because the index might be stale
                    .ToList();

                if (!awaitingDispatching.Any())
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.Debug("Nothing to dispatch. Waiting...");
                    }
                    token.WaitHandle.WaitOne(TimeSpan.FromSeconds(1));
                    return;
                }

                var failedMessageIds = awaitingDispatching
                    .Select(x => x.Payload)
                    .Cast<MessageFailed>()
                    .Select(x => x.FailedMessageId)
                    .ToArray();

                if (Logger.IsDebugEnabled)
                {
                    Logger.DebugFormat("Dispatching {0} events.",failedMessageIds.Length);
                }
                var newestEtag = awaitingDispatching.Max(x => session.Advanced.GetEtagFor(x));

                var failedMessageData = session.Query<Contracts.Failures.MessageFailed, ExternalIntegrationsFailedMessagesIndex>()
                    .Customize(c => c.WaitForNonStaleResultsAsOf(newestEtag))
                    .Where(x => x.FailedMessageId.In(failedMessageIds))
                    .ProjectFromIndexFieldsInto<Contracts.Failures.MessageFailed>()
                    .ToList();

                foreach (var messageFailed in failedMessageData)
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.DebugFormat("Publishing external event on the bus.");
                    }
                    Bus.Publish(messageFailed);
                }

                foreach (var dispatchedEvent in awaitingDispatching)
                {
                    dispatchedEvent.Dispatched = true;
                }

                session.SaveChanges();
            }
        }

        CancellationTokenSource tokenSource;

        static readonly ILog Logger = LogManager.GetLogger(typeof(EventDispatcher));
    }
}