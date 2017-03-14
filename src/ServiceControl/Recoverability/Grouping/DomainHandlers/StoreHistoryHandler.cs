namespace ServiceControl.Recoverability.Grouping.DomainHandlers
{
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.DomainEvents;

    public class StoreHistoryHandler: IDomainHandler<RetryOperationCompleted>
    {
        private readonly IDocumentStore store;
        private readonly Settings settings;

        public StoreHistoryHandler(IDocumentStore store, Settings settings)
        {
            this.store = store;
            this.settings = settings;
        }

        public void Handle(RetryOperationCompleted message)
        {
            using (var session = store.OpenSession())
            {
                var retryHistory = session.Load<RetryHistory>(RetryHistory.MakeId()) ?? RetryHistory.CreateNew();

                retryHistory.AddToHistory(new HistoricRetryOperation
                {
                    RequestId = message.RequestId,
                    RetryType = message.RetryType,
                    StartTime = message.StartTime,
                    CompletionTime = message.CompletionTime,
                    Originator = message.Originator,
                    Failed = message.Failed,
                    NumberOfMessagesProcessed = message.NumberOfMessagesProcessed
                }, settings.RetryHistoryDepth);

                retryHistory.AddToUnacknowledged(new UnacknowledgedOperation
                {
                    RequestId = message.RequestId,
                    RetryType = message.RetryType,
                    StartTime = message.StartTime,
                    CompletionTime = message.CompletionTime,
                    Originator = message.Originator,
                    Classifier = message.Classifier,
                    Failed = message.Failed,
                    NumberOfMessagesProcessed = message.NumberOfMessagesProcessed,
                    Last = message.Last,
                });

                session.Store(retryHistory);
                session.SaveChanges();
            }
        }
    }
}