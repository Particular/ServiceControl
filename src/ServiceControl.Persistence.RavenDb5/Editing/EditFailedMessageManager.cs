namespace ServiceControl.Persistence.RavenDb
{
    using System;
    using System.Threading.Tasks;
    using ServiceControl.MessageFailures;
    using ServiceControl.Persistence.Recoverability.Editing;
    using Raven.Client.Documents.Session;
    using Raven.Client;

    class EditFailedMessageManager : AbstractSessionManager, IEditFailedMessagesManager
    {
        readonly IAsyncDocumentSession session;
        readonly TimeSpan errorRetentionPeriod;
        FailedMessage failedMessage;

        public EditFailedMessageManager(IAsyncDocumentSession session, TimeSpan errorRetentionPeriod)
            : base(session)
        {
            this.session = session;
            this.errorRetentionPeriod = errorRetentionPeriod;
        }

        public async Task<FailedMessage> GetFailedMessage(string failedMessageId)
        {
            failedMessage = await session.LoadAsync<FailedMessage>(FailedMessageIdGenerator.MakeDocumentId(failedMessageId));
            return failedMessage;
        }

        public async Task<string> GetCurrentEditingMessageId(string failedMessageId)
        {
            var edit = await session.LoadAsync<FailedMessageEdit>(FailedMessageEdit.MakeDocumentId(failedMessageId));
            return edit?.EditId;
        }

        public Task SetCurrentEditingMessageId(string editingMessageId)
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
            var expiresAt = DateTime.UtcNow + errorRetentionPeriod;
            session.Advanced.GetMetadataFor(failedMessage)[Constants.Documents.Metadata.Expires] = expiresAt;
            return Task.CompletedTask;
        }
    }
}
