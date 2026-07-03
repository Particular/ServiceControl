namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Infrastructure.Auth;
    using MessageFailures.InternalMessages;
    using NServiceBus;
    using Persistence;

    [Handler]
    class PendingRetriesHandler : IHandleMessages<RetryPendingMessagesById>,
        IHandleMessages<RetryPendingMessages>
    {
        public PendingRetriesHandler(IErrorMessageDataStore dataStore, IMessageActionAuditLog auditLog)
        {
            this.dataStore = dataStore;
            this.auditLog = auditLog;
        }

        public async Task Handle(RetryPendingMessages message, IMessageHandlerContext context)
        {
            var messageIds = new List<string>();

            var ids = await dataStore.GetRetryPendingMessages(message.PeriodFrom, message.PeriodTo, message.QueueAddress);

            var (user, operationId) = AuditHeaders.Read(context.MessageHeaders);

            foreach (var id in ids)
            {
                await dataStore.RemoveFailedMessageRetryDocument(id);
                messageIds.Add(id);

                if (!string.IsNullOrEmpty(operationId))
                {
                    auditLog.MessageAction(user, MessageActionKind.Retry, Permissions.ErrorMessagesRetry, MessageActionScope.Queue, id, operationId);
                }
            }

            await context.SendLocal(new RetryMessagesById { MessageUniqueIds = messageIds.ToArray() });
        }

        public async Task Handle(RetryPendingMessagesById message, IMessageHandlerContext context)
        {
            var (user, operationId) = AuditHeaders.Read(context.MessageHeaders);

            foreach (var messageUniqueId in message.MessageUniqueIds)
            {
                await dataStore.RemoveFailedMessageRetryDocument(messageUniqueId);

                if (!string.IsNullOrEmpty(operationId))
                {
                    auditLog.MessageAction(user, MessageActionKind.Retry, Permissions.ErrorMessagesRetry, MessageActionScope.Batch, messageUniqueId, operationId);
                }
            }

            await context.SendLocal<RetryMessagesById>(m => m.MessageUniqueIds = message.MessageUniqueIds);
        }

        readonly IErrorMessageDataStore dataStore;
        readonly IMessageActionAuditLog auditLog;
    }
}