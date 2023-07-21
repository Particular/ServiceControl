namespace ServiceControl.Persistence.RavenDb
{
    using System.Threading.Tasks;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using ServiceControl.MessageFailures;
    using ServiceControl.Persistence.Recoverability.Editing;

    class EditFailedMessageManager : AbstractSessionManager, IEditFailedMessagesManager
    {
        readonly IAsyncDocumentSession session;
        FailedMessage failedMessage;

        public EditFailedMessageManager(IAsyncDocumentSession session)
            : base(session)
        {
            this.session = session;
        }

        public async Task<FailedMessage> GetFailedMessage(string failedMessageId)
        {
            failedMessage = await session.LoadAsync<FailedMessage>(FailedMessage.MakeDocumentId(failedMessageId));
            return failedMessage;
        }

        public async Task<string> GetCurrentEditingMessageId(string failedMessageId)
        {
            var edit = await session.LoadAsync<FailedMessageEdit>(FailedMessageEdit.MakeDocumentId(failedMessageId));

            return edit?.Id;
        }

        public Task SetCurrentEditingMessageId(string editingMessageId)
        {
            return session.StoreAsync(new FailedMessageEdit
            {
                Id = FailedMessageEdit.MakeDocumentId(failedMessage.Id),
                FailedMessageId = failedMessage.Id,
                EditId = editingMessageId
            }, Etag.Empty);
        }

        public Task SetFailedMessageAsResolved()
        {
            // Instance is tracked by the document session
            failedMessage.Status = FailedMessageStatus.Resolved;
            return Task.CompletedTask;
        }
    }
}
