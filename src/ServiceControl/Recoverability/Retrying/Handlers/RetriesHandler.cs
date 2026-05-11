namespace ServiceControl.Recoverability
{
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using MessageFailures.InternalMessages;
    using NServiceBus;
    using ServiceControl.Persistence;

    [Handler]
    class RetriesHandler(RetriesGateway retries, IErrorMessageDataStore dataStore) : IHandleMessages<RequestRetryAll>,
        IHandleMessages<RetryMessagesById>,
        IHandleMessages<RetryMessage>,
        IHandleMessages<MessageFailed>,
        IHandleMessages<RetryMessagesByQueueAddress>
    {
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

        public Task Handle(RetryMessage message, IMessageHandlerContext context) => retries.StartRetryForSingleMessage(message.FailedMessageId);

        public Task Handle(RetryMessagesById message, IMessageHandlerContext context) => retries.StartRetryForMessageSelection(message.MessageUniqueIds);

        public Task Handle(RetryMessagesByQueueAddress message, IMessageHandlerContext context)
        {
            var failedQueueAddress = message.QueueAddress;

            retries.StartRetryForFailedQueueAddress(failedQueueAddress, message.Status);

            return Task.CompletedTask;
        }
    }
}