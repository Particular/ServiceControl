using ServiceControl.MessageFailures;

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
                return RetryDocumentManager.RemoveFailedMessageRetryDocument(FailedMessage.MakeDocumentId(message.FailedMessageId));
            }

            return Task.FromResult(0);
        }
    }
}