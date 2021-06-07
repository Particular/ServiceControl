namespace ServiceControl.Recoverability
{
    using System;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using MessageFailures;
    using MessageFailures.Api;
    using MessageFailures.InternalMessages;
    using NServiceBus;

    class RetriesHandler : IHandleMessages<RequestRetryAll>,
        IHandleMessages<RetryMessagesById>,
        IHandleMessages<RetryMessage>,
        IHandleMessages<MessageFailed>,
        IHandleMessages<RetryMessagesByQueueAddress>
    {
        readonly RetriesGateway retries;
        readonly RetryDocumentManager retryDocumentManager;

        public RetriesHandler(RetriesGateway retries, RetryDocumentManager retryDocumentManager)
        {
            this.retries = retries;
            this.retryDocumentManager = retryDocumentManager;
        }
        /// <summary>
        /// For handling leftover messages. MessageFailed are no longer published on the bus and the code is moved to
        /// <see cref="FailedMessageRetryCleaner" />.
        /// </summary>
        public Task Handle(MessageFailed message, IMessageHandlerContext context)
        {
            if (message.RepeatedFailure)
            {
                return retryDocumentManager.RemoveFailedMessageRetryDocument(message.FailedMessageId);
            }

            return Task.FromResult(0);
        }

        public Task Handle(RequestRetryAll message, IMessageHandlerContext context)
        {
            if (!string.IsNullOrWhiteSpace(message.Endpoint))
            {
                retries.StartRetryForIndex<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>(message.Endpoint, RetryType.AllForEndpoint, DateTime.UtcNow, m => m.ReceivingEndpointName == message.Endpoint, "all messages for endpoint " + message.Endpoint);
            }
            else
            {
                retries.StartRetryForIndex<FailedMessage, FailedMessageViewIndex>("All", RetryType.All, DateTime.UtcNow, originator: "all messages");
            }

            return Task.FromResult(0);
        }

        public Task Handle(RetryMessage message, IMessageHandlerContext context)
        {
            return retries.StartRetryForSingleMessage(message.FailedMessageId);
        }

        public Task Handle(RetryMessagesById message, IMessageHandlerContext context)
        {
            return retries.StartRetryForMessageSelection(message.MessageUniqueIds);
        }

        public Task Handle(RetryMessagesByQueueAddress message, IMessageHandlerContext context)
        {
            var failedQueueAddress = message.QueueAddress;

            retries.StartRetryForIndex<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>(failedQueueAddress, RetryType.ByQueueAddress, DateTime.UtcNow, m => m.QueueAddress == failedQueueAddress && m.Status == message.Status, $"all messages for failed queue address '{message.QueueAddress}'");

            return Task.FromResult(0);
        }
    }
}