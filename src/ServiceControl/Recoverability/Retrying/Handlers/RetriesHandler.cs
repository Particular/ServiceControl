namespace ServiceControl.Recoverability
{
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Infrastructure.Auth;
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
            var (user, operationId) = AuditHeaders.Read(context.MessageHeaders);

            if (!string.IsNullOrWhiteSpace(message.Endpoint))
            {
                retries.StartRetryForEndpoint(message.Endpoint, user, operationId);
            }
            else
            {
                retries.StartRetryForAllMessages(user, operationId);
            }

            return Task.CompletedTask;
        }

        public Task Handle(RetryMessage message, IMessageHandlerContext context)
        {
            var (user, operationId) = AuditHeaders.Read(context.MessageHeaders);
            return retries.StartRetryForSingleMessage(message.FailedMessageId, user, operationId);
        }

        public Task Handle(RetryMessagesById message, IMessageHandlerContext context)
        {
            var (user, operationId) = AuditHeaders.Read(context.MessageHeaders);
            return retries.StartRetryForMessageSelection(message.MessageUniqueIds, user, operationId);
        }

        public Task Handle(RetryMessagesByQueueAddress message, IMessageHandlerContext context)
        {
            var failedQueueAddress = message.QueueAddress;

            var (user, operationId) = AuditHeaders.Read(context.MessageHeaders);
            retries.StartRetryForFailedQueueAddress(failedQueueAddress, message.Status, user, operationId);

            return Task.CompletedTask;
        }
    }
}