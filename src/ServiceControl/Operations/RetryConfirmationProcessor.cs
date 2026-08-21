namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Infrastructure.DomainEvents;
    using NServiceBus;
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

        public async Task Process(List<MessageContext> contexts, IIngestionUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
        {
            foreach (var context in contexts)
            {
                var retriedMessageUniqueId = context.Headers[RetryUniqueMessageIdHeader];
                await unitOfWork.Recoverability.RecordSuccessfulRetry(retriedMessageUniqueId, GetSucceededAt(context.Headers), cancellationToken);
            }
        }

        public Task Announce(MessageContext messageContext, CancellationToken cancellationToken = default)
        {
            return domainEvents.Raise(new MessageFailureResolvedByRetry
            {
                FailedMessageId = messageContext.Headers[RetryUniqueMessageIdHeader],
            }, cancellationToken);
        }

        // An acknowledgement that carries no readable time leaves nothing to order the confirmation
        // against, so it is taken to be the most recent thing that happened to the message. That is
        // how every confirmation was treated before the time was read at all.
        static DateTime GetSucceededAt(Dictionary<string, string> headers)
        {
            if (headers.TryGetValue(SuccessfulRetryHeader, out var wireFormattedTime) && !string.IsNullOrWhiteSpace(wireFormattedTime))
            {
                try
                {
                    return DateTimeOffsetHelper.ToDateTimeOffset(wireFormattedTime).UtcDateTime;
                }
                catch (FormatException)
                {
                }
            }

            return NewerThanAnyAttempt;
        }

        static readonly DateTime NewerThanAnyAttempt = DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);

        readonly IDomainEvents domainEvents;
    }
}