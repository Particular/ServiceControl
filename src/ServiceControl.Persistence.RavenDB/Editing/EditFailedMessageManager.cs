namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Session;
    using ServiceControl.MessageFailures;
    using ServiceControl.Persistence.Recoverability.Editing;
    using ServiceControl.Persistence.RavenDB.Indexes;

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

        public async Task<FailedMessage> GetFailedMessage(string failedMessageId)
        {
            failedMessage = await session.LoadAsync<FailedMessage>(FailedMessageIdGenerator.MakeDocumentId(failedMessageId));
            return failedMessage;
        }

        public async Task<string> GetCurrentEditingRequestId(string failedMessageId)
        {
            var edit = await session.LoadAsync<FailedMessageEdit>(FailedMessageEdit.MakeDocumentId(failedMessageId));
            return edit?.EditId;
        }

        public Task SetCurrentEditingRequestId(string editingMessageId)
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
            });
        }

        public Task SetFailedMessageAsResolved()
        {
            // Instance is tracked by the document session
            failedMessage.Status = FailedMessageStatus.Resolved;

            expirationManager.EnableExpiration(session, failedMessage);

            return Task.CompletedTask;
        }

        public async Task<string> GetFailedMessageIdByEditId(string editId)
        {
            var edit = await session.Query<FailedMessageEdit, FailedMessageEditIndex>()
                .Where(x => x.EditId == editId)
                .FirstOrDefaultAsync();

            if (edit?.FailedMessageId != null)
            {
                return FailedMessageIdGenerator.GetMessageIdFromDocumentId(edit.FailedMessageId);
            }

            return null;
        }
    }
}
