namespace ServiceControl.Recoverability
{
    using System.Threading;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Infrastructure.DomainEvents;
    using Persistence;

    class FailedMessageRetryCleaner : IDomainHandler<MessageFailed>
    {
        readonly IFailedMessageRetryDataStore dataStore;

        public FailedMessageRetryCleaner(IFailedMessageRetryDataStore dataStore)
        {
            this.dataStore = dataStore;
        }

        public Task Handle(MessageFailed message, CancellationToken cancellationToken = default)
        {
            if (message.RepeatedFailure)
            {
                return dataStore.RemoveFailedMessageRetry(message.FailedMessageId);
            }

            return Task.CompletedTask;
        }
    }
}