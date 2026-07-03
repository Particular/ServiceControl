namespace ServiceControl.MessageFailures.Handlers
{
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Infrastructure.Auth;
    using Infrastructure.DomainEvents;
    using InternalMessages;
    using NServiceBus;
    using ServiceControl.Persistence;

    [Handler]
    class ArchiveMessageHandler(IErrorMessageDataStore dataStore, IDomainEvents domainEvents, IMessageActionAuditLog auditLog) : IHandleMessages<ArchiveMessage>
    {
        public async Task Handle(ArchiveMessage message, IMessageHandlerContext context)
        {
            var failedMessageId = message.FailedMessageId;

            var failedMessage = await dataStore.ErrorBy(failedMessageId);

            if (failedMessage.Status != FailedMessageStatus.Archived)
            {
                await domainEvents.Raise(new FailedMessageArchived
                {
                    FailedMessageId = failedMessageId
                }, context.CancellationToken);

                await dataStore.FailedMessageMarkAsArchived(failedMessageId);

                var (user, operationId) = AuditHeaders.Read(context.MessageHeaders);
                if (!string.IsNullOrEmpty(operationId))
                {
                    auditLog.MessageAction(user, MessageActionKind.Archive, Permissions.ErrorMessagesArchive, MessageActionScope.Single, failedMessageId, operationId);
                }
            }
        }
    }
}