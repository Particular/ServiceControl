namespace ServiceControl.Operations
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Infrastructure.DomainEvents;
    using NServiceBus.Transport;

    class RetryConfirmationProcessor
    {
        public const string SuccessfulRetryHeader = "ServiceControl.Retry.Successful";
        const string RetryUniqueMessageIdHeader = "ServiceControl.Retry.UniqueMessageId";

        public RetryConfirmationProcessor(IDomainEvents domainEvents)
        {
            this.domainEvents = domainEvents;
        }

        public void Process(List<MessageContext> contexts, IIngestionUnitOfWork unitOfWork)
        {
            foreach (var context in contexts)
            {
                var retriedMessageUniqueId = context.Headers[RetryUniqueMessageIdHeader];
                unitOfWork.RecordSuccessfulRetry(retriedMessageUniqueId);
            }
        }

        public Task Announce(MessageContext messageContext)
        {
            return domainEvents.Raise(new MessageFailureResolvedByRetry
            {
                FailedMessageId = messageContext.Headers[RetryUniqueMessageIdHeader],
            });
        }

        readonly IDomainEvents domainEvents;
    }
}