namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.MessageFailures.InternalMessages;

    public class RetriesHandler : IHandleMessages<RequestRetryAll>,
        IHandleMessages<RetryMessagesById>,
        IHandleMessages<RetryMessage>,
        IHandleMessages<MessageFailedRepeatedly>,
        IHandleMessages<MessageFailed>,
        IHandleMessages<RetryMessagesByQueueAddress>
    {
        public RetriesGateway Retries { get; set; }
        public RetryDocumentManager RetryDocumentManager { get; set; }

        public Task Handle(RequestRetryAll message, IMessageHandlerContext context)
        {
            if (!string.IsNullOrWhiteSpace(message.Endpoint))
            {
                Retries.StartRetryForIndex<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>(message.Endpoint, RetryType.AllForEndpoint, DateTime.UtcNow, m => m.ReceivingEndpointName == message.Endpoint, "all messages for endpoint " + message.Endpoint);
            }
            else
            {
                Retries.StartRetryForIndex<FailedMessage, FailedMessageViewIndex>("All", RetryType.All, DateTime.UtcNow, originator: "all messages");
            }
            return Task.FromResult(0);
        }

        public Task Handle(RetryMessagesById message, IMessageHandlerContext context)
        {
            return Retries.StartRetryForMessageSelection(message.MessageUniqueIds);
        }

        public Task Handle(RetryMessage message, IMessageHandlerContext context)
        {
            return Retries.StartRetryForSingleMessage(message.FailedMessageId);
        }

        public Task Handle(MessageFailedRepeatedly message, IMessageHandlerContext context)
        {
            return RetryDocumentManager.RemoveFailedMessageRetryDocument(message.FailedMessageId);
        }

        public Task Handle(MessageFailed message, IMessageHandlerContext context)
        {
            if (message.RepeatedFailure)
            {
                return RetryDocumentManager.RemoveFailedMessageRetryDocument(message.FailedMessageId);
            }
            return Task.FromResult(0);
        }

        public Task Handle(RetryMessagesByQueueAddress message, IMessageHandlerContext context)
        {
            var failedQueueAddress = message.QueueAddress;

            Retries.StartRetryForIndex<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>(failedQueueAddress, RetryType.ByQueueAddress, DateTime.UtcNow, m => m.QueueAddress == failedQueueAddress && m.Status == message.Status, $"all messages for failed queue address '{message.QueueAddress}'");

            return Task.FromResult(0);
        }
    }
}