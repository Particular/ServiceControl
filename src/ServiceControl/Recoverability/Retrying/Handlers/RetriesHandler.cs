namespace ServiceControl.Recoverability
{
    using System;
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
            if (!string.IsNullOrWhiteSpace(message.Endpoint))
            {
                Retries.StartRetryForIndex<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>(message.Endpoint, RetryType.AllForEndpoint, DateTime.UtcNow, m => m.ReceivingEndpointName == message.Endpoint, "all messages for endpoint " + message.Endpoint);
            }
            else
            {
                Retries.StartRetryForIndex<FailedMessage, FailedMessageViewIndex>("All", RetryType.All, DateTime.UtcNow, originator: "all messages");
            }
        }

        public void Handle(RetryMessagesById message)
        {
            Retries.StageRetryByUniqueMessageIds(Guid.NewGuid().ToString(), RetryType.MultipleMessages, message.MessageUniqueIds, DateTime.UtcNow);
        }

        public void Handle(RetryMessage message)
        {
            Retries.StageRetryByUniqueMessageIds(message.FailedMessageId, RetryType.SingleMessage, new[] { message.FailedMessageId }, DateTime.UtcNow);
        }

        public void Handle(MessageFailedRepeatedly message)
        {
            RetryDocumentManager.RemoveFailedMessageRetryDocument(message.FailedMessageId);
        }

        public void Handle(RetryMessagesByQueueAddress message)
        {
            var failedQueueAddress = message.QueueAddress;

            Retries.StartRetryForIndex<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>(failedQueueAddress, RetryType.ByQueueAddress, DateTime.UtcNow, m => m.QueueAddress == failedQueueAddress && m.Status == message.Status, $"all messages for failed queue address '{message.QueueAddress}'");
        }
    }
}