namespace ServiceControl.Recoverability
{
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Infrastructure.DomainEvents;

    class FailedMessageRetryCleaner : IDomainHandler<MessageFailed>
    {
        public RetryDocumentManager RetryDocumentManager { get; set; }

        public Task Handle(MessageFailed message)
        {
            if (message.RepeatedFailure)
            {
                //TODO: RAVEN5 check if failedMessageId contains collection name
                return RetryDocumentManager.RemoveFailedMessageRetryDocument(message.FailedMessageId);
            }

            return Task.FromResult(0);
        }
    }
}