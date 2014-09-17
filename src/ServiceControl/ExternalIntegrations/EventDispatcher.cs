namespace ServiceControl.ExternalIntegrations
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
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
                    token.WaitHandle.WaitOne(TimeSpan.FromSeconds(1));
                    return;
                }

                var fialedMessageIds = awaitingDispatching
                    .Select(x => x.Payload)
                    .Cast<MessageFailed>()
                    .Select(x => x.FailedMessageId)
                    .ToArray();

                var newestEtag = awaitingDispatching.Max(x => session.Advanced.GetEtagFor(x));

                var failedMessageData = session.Query<Contracts.Failures.MessageFailed, ExternalIntegrationsFailedMessagesIndex>()
                    .Customize(c => c.WaitForNonStaleResultsAsOf(newestEtag))
                    .Where(x => x.FailedMessageId.In(fialedMessageIds))
                    .ProjectFromIndexFieldsInto<Contracts.Failures.MessageFailed>()
                    .ToList();

                foreach (var messageFailed in failedMessageData)
                {
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
    }
}