namespace ServiceControl.MessageFailures.Handlers
{
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Infrastructure.Auth;
    using Infrastructure.DomainEvents;
    using InternalMessages;
    using NServiceBus;
    using Persistence;

    [Handler]
    class UnArchiveMessagesHandler(IErrorMessageDataStore store, IDomainEvents domainEvents, IMessageActionAuditLog auditLog)
        : IHandleMessages<UnArchiveMessages>
    {
        public async Task Handle(UnArchiveMessages messages, IMessageHandlerContext context)
        {
            var ids = await store.UnArchiveMessages(messages.FailedMessageIds);

            var (user, operationId) = AuditHeaders.Read(context.MessageHeaders);
            if (!string.IsNullOrEmpty(operationId))
            {
                foreach (var id in ids)
                {
                    // ids are Raven document ids (FailedMessages/{uniqueId}); audit records the bare unique id
                    auditLog.MessageAction(user, MessageActionKind.Unarchive, Permissions.ErrorMessagesUnarchive, MessageActionScope.Batch, id.Replace("FailedMessages/", ""), operationId);
                }
            }

            await domainEvents.Raise(new FailedMessagesUnArchived
            {
                DocumentIds = ids,
                MessagesCount = ids.Length
            }, context.CancellationToken);
        }
    }
}