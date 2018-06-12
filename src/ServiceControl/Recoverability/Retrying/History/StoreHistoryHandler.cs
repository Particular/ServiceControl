namespace ServiceControl.Recoverability.Retrying
{
    using System.Threading.Tasks;
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

        public async Task Handle(RetryOperationCompleted message)
        {
            using (var session = store.OpenAsyncSession())
            {
                var retryHistory = await session.LoadAsync<RetryHistory>(RetryHistory.MakeId()).ConfigureAwait(false) ?? 
                                   RetryHistory.CreateNew();

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

                retryHistory.AddToUnacknowledged(new UnacknowledgedRetryOperation
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

                await session.StoreAsync(retryHistory)
                    .ConfigureAwait(false);
                await session.SaveChangesAsync()
                    .ConfigureAwait(false);
            }
        }
    }
}