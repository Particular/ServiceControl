namespace ServiceControl.Operations
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Infrastructure.DomainEvents;
    using NServiceBus.Transport;
    using ServiceControl.Persistence.UnitOfWork;

    class RetryConfirmationProcessor
    {
        public const string SuccessfulRetryHeader = "ServiceControl.Retry.Successful";
        const string RetryUniqueMessageIdHeader = "ServiceControl.Retry.UniqueMessageId";


        public RetryConfirmationProcessor(IDomainEvents domainEvents)
        {
            this.domainEvents = domainEvents;
        }

        public async Task Process(List<MessageContext> contexts, IIngestionUnitOfWork unitOfWork)
        {
            foreach (var context in contexts)
            {
                var retriedMessageUniqueId = context.Headers[RetryUniqueMessageIdHeader];
                await unitOfWork.Recoverability.RecordSuccessfulRetry(retriedMessageUniqueId);
            }
        }

        public Task Announce(string failedMessageId)
        {
            return domainEvents.Raise(new MessageFailureResolvedByRetry
            {
                FailedMessageId = failedMessageId
            });
        }

        readonly IDomainEvents domainEvents;
    }
}