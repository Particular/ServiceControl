namespace ServiceControl.Recoverability.Retrying
{
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence;

    public class StoreHistoryHandler : IDomainHandler<RetryOperationCompleted>
    {
        public StoreHistoryHandler(IErrorMessageDataStore store, Settings settings)
        {
            this.store = store;
            this.settings = settings;
        }

        public Task Handle(RetryOperationCompleted message)
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

        readonly IErrorMessageDataStore store;
        readonly Settings settings;
    }
}