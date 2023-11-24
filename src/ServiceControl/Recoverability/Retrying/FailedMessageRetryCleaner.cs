namespace ServiceControl.Recoverability
{
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Infrastructure.DomainEvents;
    using Persistence;

    class FailedMessageRetryCleaner : IDomainHandler<MessageFailed>
    {
        readonly IErrorMessageDataStore dataStore;

        public FailedMessageRetryCleaner(IErrorMessageDataStore dataStore)
        {
            this.dataStore = dataStore;
        }

        public Task Handle(MessageFailed message)
        {
            if (message.RepeatedFailure)
            {
                return dataStore.RemoveFailedMessageRetryDocument(message.FailedMessageId);
            }

            return Task.CompletedTask;
        }
    }
}