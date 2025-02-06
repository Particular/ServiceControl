namespace ServiceControl.Recoverability.Retrying
{
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence;

    public class StoreHistoryHandler : IDomainHandler<RetryOperationCompleted>
    {
        public StoreHistoryHandler(IRetryHistoryDataStore store, Settings settings)
        {
            this.store = store;
            this.settings = settings;
        }

        public Task Handle(RetryOperationCompleted message, CancellationToken cancellationToken)
        {
            return store.RecordRetryOperationCompleted(
                message.RequestId,
                message.RetryType,
                message.StartTime,
                message.CompletionTime,
                message.Originator,
                message.Classifier,
                message.Failed,
                message.NumberOfMessagesProcessed,
                message.Last,
                settings.RetryHistoryDepth);
        }

        readonly IRetryHistoryDataStore store;
        readonly Settings settings;
    }
}