namespace ServiceControl.Persistence.RavenDb
{
    using System.Threading.Tasks;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using ServiceControl.MessageFailures;
    using ServiceControl.Persistence.Recoverability.Editing;

    class EditFailedMessageManager : AbstractEditFailedMessagesManager
    {
        readonly IAsyncDocumentSession session;

        public EditFailedMessageManager(IAsyncDocumentSession session, FailedMessage failedMessage)
            : base(failedMessage)
        {
            this.session = session;
        }

        public override async Task<string> GetCurrentEditingMessageId()
        {
            var edit = await session.LoadAsync<FailedMessageEdit>(FailedMessageEdit.MakeDocumentId(FailedMessage.Id))
                .ConfigureAwait(false);

            return edit?.Id;
        }

        public override Task SetCurrentEditingMessageId(string editingMessageId)
        {
            return session.StoreAsync(new FailedMessageEdit
            {
                Id = FailedMessageEdit.MakeDocumentId(FailedMessage.Id),
                FailedMessageId = FailedMessage.Id,
                EditId = editingMessageId
            }, Etag.Empty);
        }

        public override Task SetFailedMessageAsResolved()
        {
            // Instance is tracked by the document session
            FailedMessage.Status = FailedMessageStatus.Resolved;
            return Task.CompletedTask;
        }

        public override Task SaveChanges()
        {
            return session.SaveChangesAsync();
        }
    }
}
