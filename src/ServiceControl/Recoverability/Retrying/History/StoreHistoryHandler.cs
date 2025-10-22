namespace ServiceControl.Recoverability.Retrying
{
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.Options;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence;

    public class StoreHistoryHandler(IRetryHistoryDataStore store, IOptions<PrimaryOptions> options)
        : IDomainHandler<RetryOperationCompleted>
    {
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
                options.Value.RetryHistoryDepth
            );
        }
    }
}