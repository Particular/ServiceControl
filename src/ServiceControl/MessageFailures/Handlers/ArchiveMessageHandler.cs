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
    class ArchiveMessageHandler(IFailedMessageQueryDataStore queryStore, IFailedMessageLifecycleDataStore lifecycleStore, IDomainEvents domainEvents, IMessageActionAuditLog auditLog) : IHandleMessages<ArchiveMessage>
    {
        public async Task Handle(ArchiveMessage message, IMessageHandlerContext context)
        {
            var failedMessageId = message.FailedMessageId;

            var failedMessage = await queryStore.GetFailedMessage(failedMessageId, context.CancellationToken);

            if (failedMessage.Status != FailedMessageStatus.Archived)
            {
                await domainEvents.Raise(new FailedMessageArchived
                {
                    FailedMessageId = failedMessageId
                }, context.CancellationToken);

                await lifecycleStore.MarkAsArchived(failedMessageId, context.CancellationToken);

                var (user, operationId) = AuditHeaders.Read(context.MessageHeaders);
                if (!string.IsNullOrEmpty(operationId))
                {
                    auditLog.MessageAction(user, MessageActionKind.Archive, Permissions.ErrorMessagesArchive, message.Scope, failedMessageId, operationId);
                }
            }
        }
    }
}