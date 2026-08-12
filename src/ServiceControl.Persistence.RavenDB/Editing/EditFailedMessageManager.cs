namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents.Session;
    using ServiceControl.MessageFailures;
    using ServiceControl.Persistence.Recoverability.Editing;

    class EditFailedMessageManager : AbstractSessionManager, IEditFailedMessagesManager
    {
        readonly IAsyncDocumentSession session;
        readonly ExpirationManager expirationManager;
        FailedMessage failedMessage;

        public EditFailedMessageManager(IAsyncDocumentSession session, ExpirationManager expirationManager)
            : base(session)
        {
            this.session = session;
            this.expirationManager = expirationManager;
        }

        public async Task<FailedMessage> GetFailedMessage(string failedMessageId, CancellationToken cancellationToken = default)
        {
            failedMessage = await session.LoadAsync<FailedMessage>(FailedMessageIdGenerator.MakeDocumentId(failedMessageId), cancellationToken);
            return failedMessage;
        }

        public async Task<string> GetCurrentEditingRequestId(string failedMessageId, CancellationToken cancellationToken = default)
        {
            var edit = await session.LoadAsync<FailedMessageEdit>(FailedMessageEdit.MakeDocumentId(failedMessageId), cancellationToken);
            return edit?.EditId;
        }

        public Task SetCurrentEditingRequestId(string editingMessageId, CancellationToken cancellationToken = default)
        {
            if (failedMessage == null)
            {
                throw new InvalidOperationException("No failed message loaded");
            }
            return session.StoreAsync(new FailedMessageEdit
            {
                Id = FailedMessageEdit.MakeDocumentId(failedMessage.UniqueMessageId),
                FailedMessageId = failedMessage.Id,
                EditId = editingMessageId
            }, cancellationToken);
        }

        public Task SetFailedMessageAsResolved(CancellationToken cancellationToken = default)
        {
            // Instance is tracked by the document session
            failedMessage.Status = FailedMessageStatus.Resolved;

            expirationManager.EnableExpiration(session, failedMessage);

            return Task.CompletedTask;
        }
    }
}
