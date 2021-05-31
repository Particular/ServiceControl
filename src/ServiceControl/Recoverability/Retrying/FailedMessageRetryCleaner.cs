namespace ServiceControl.Recoverability
{
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Infrastructure.DomainEvents;

    class FailedMessageRetryCleaner : IDomainHandler<MessageFailed>
    {
        readonly RetryDocumentManager retryDocumentManager;

        public FailedMessageRetryCleaner(RetryDocumentManager retryDocumentManager)
        {
            this.retryDocumentManager = retryDocumentManager;
        }

        public Task Handle(MessageFailed message)
        {
            if (message.RepeatedFailure)
            {
                return retryDocumentManager.RemoveFailedMessageRetryDocument(message.FailedMessageId);
            }

            return Task.FromResult(0);
        }
    }
}