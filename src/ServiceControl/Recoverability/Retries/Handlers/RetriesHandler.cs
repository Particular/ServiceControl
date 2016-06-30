namespace ServiceControl.Recoverability
{
    using NServiceBus;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.MessageFailures.InternalMessages;

    public class RetriesHandler : IHandleMessages<RequestRetryAll>,
        IHandleMessages<RetryMessagesById>,
        IHandleMessages<RetryMessage>,
        IHandleMessages<MessageFailedRepeatedly>,
        IHandleMessages<RetryMessagesByQueueAddress>
    {
        public RetriesGateway Retries { get; set; }
        public RetryDocumentManager RetryDocumentManager { get; set; }

        public void Handle(RequestRetryAll message)
        {
            if (message.Endpoint != null)
            {
                Retries.StartRetryForIndex<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>(m => m.ReceivingEndpointName == message.Endpoint, "all messages for endpoint " + message.Endpoint);
            }
            else
            {
                Retries.StartRetryForIndex<FailedMessage, FailedMessageViewIndex>(context: "all messages");
            }
        }

        public void Handle(RetryMessagesById message)
        {
            Retries.StageRetryByUniqueMessageIds(message.MessageUniqueIds);
        }

        public void Handle(RetryMessage message)
        {
            Retries.StageRetryByUniqueMessageIds(new [] { message.FailedMessageId });
        }

        public void Handle(MessageFailedRepeatedly message)
        {
            RetryDocumentManager.RemoveFailedMessageRetryDocument(message.FailedMessageId);
        }

        public void Handle(RetryMessagesByQueueAddress message)
        {
            var failedQueueAddress = message.QueueAddress.ToLowerInvariant();

            Retries.StartRetryForIndex<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>(m => m.QueueAddress == failedQueueAddress && m.Status == message.Status, "all messages for failed queue address" + message.QueueAddress);
        }
    }
}