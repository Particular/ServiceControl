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
        readonly IErrorMessageDataStore dataStore;

        public RetriesHandler(RetriesGateway retries, IErrorMessageDataStore dataStore)
        {
            this.retries = retries;
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

            return Task.CompletedTask;
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

            return Task.CompletedTask;
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

            return Task.CompletedTask;
        }
    }
}