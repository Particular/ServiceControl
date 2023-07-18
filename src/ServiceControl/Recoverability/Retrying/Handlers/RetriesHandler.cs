namespace ServiceControl.Recoverability
{
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using MessageFailures.InternalMessages;
    using NServiceBus;
    using ServiceControl.Persistence;

    class RetriesHandler : IHandleMessages<RequestRetryAll>,
        IHandleMessages<RetryMessagesById>,
        IHandleMessages<RetryMessage>,
        IHandleMessages<MessageFailed>,
        IHandleMessages<RetryMessagesByQueueAddress>
    {
        readonly RetriesGateway retries;
        readonly RetryDocumentManager retryDocumentManager;
        readonly IErrorMessageDataStore dataStore;

        public RetriesHandler(RetriesGateway retries, RetryDocumentManager retryDocumentManager, IErrorMessageDataStore dataStore)
        {
            this.retries = retries;
            this.retryDocumentManager = retryDocumentManager;
            this.dataStore = dataStore;
        }
        /// <summary>
        /// For handling leftover messages. MessageFailed are no longer published on the bus and the code is moved to
        /// <see cref="FailedMessageRetryCleaner" />.
        /// </summary>
        public Task Handle(MessageFailed message, IMessageHandlerContext context)
        {
            if (message.RepeatedFailure)
            {
                return dataStore.RemoveFailedMessageRetryDocument(message.FailedMessageId);
            }

            return Task.FromResult(0);
        }

        public Task Handle(RequestRetryAll message, IMessageHandlerContext context)
        {
            if (!string.IsNullOrWhiteSpace(message.Endpoint))
            {
                retries.StartRetryForEndpoint(message.Endpoint);
            }
            else
            {
                retries.StartRetryForAllMessages();
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

            retries.StartRetryForFailedQueueAddress(failedQueueAddress, message.Status);

            return Task.FromResult(0);
        }
    }
}