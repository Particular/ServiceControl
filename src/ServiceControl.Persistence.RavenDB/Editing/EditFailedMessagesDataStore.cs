namespace ServiceControl.Persistence.RavenDB.Editing
{
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Exceptions;
    using ServiceControl.MessageFailures;
    using ServiceControl.Persistence.Recoverability.Editing;

    class EditFailedMessagesDataStore(IRavenSessionProvider sessionProvider, ExpirationManager expirationManager) : IEditFailedMessagesDataStore
    {
        public async Task<string> GetCurrentEditingRequestId(string failedMessageId, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var edit = await session.LoadAsync<FailedMessageEdit>(FailedMessageEdit.MakeDocumentId(failedMessageId), cancellationToken);
            return edit?.EditId;
        }

        public async Task<BeginEditResult> TryBeginEdit(string failedMessageId, string editingMessageId, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            session.Advanced.UseOptimisticConcurrency = true;

            var failedMessage = await session.LoadAsync<FailedMessage>(FailedMessageIdGenerator.MakeDocumentId(failedMessageId), cancellationToken);
            if (failedMessage is null)
            {
                return new BeginEditResult(BeginEditOutcome.MessageNotFound);
            }
            if (failedMessage.Status != FailedMessageStatus.Unresolved)
            {
                return new BeginEditResult(BeginEditOutcome.MessageNotUnresolved);
            }

            var editDocumentId = FailedMessageEdit.MakeDocumentId(failedMessageId);
            var existingEdit = await session.LoadAsync<FailedMessageEdit>(editDocumentId, cancellationToken);
            if (existingEdit is null)
            {
                await session.StoreAsync(new FailedMessageEdit { Id = editDocumentId, FailedMessageId = failedMessage.Id, EditId = editingMessageId }, cancellationToken);

                failedMessage.Status = FailedMessageStatus.Resolved;
                expirationManager.EnableExpiration(session, failedMessage);

                try
                {
                    await session.SaveChangesAsync(cancellationToken);
                    return new(BeginEditOutcome.Acquired, failedMessage);
                }
                catch (ConcurrencyException)
                {
                    // One bounded reload is sufficient: Raven reports the conflict only after the
                    // competing atomic batch has won, so the persisted claim identifies the outcome.
                    existingEdit = await ReloadConflictResult(failedMessageId, cancellationToken);
                }
            }

            if (existingEdit.EditId == editingMessageId)
            {
                return new BeginEditResult(BeginEditOutcome.Acquired, failedMessage, existingEdit.EditId);
            }
            return new BeginEditResult(BeginEditOutcome.AcquiredByAnotherEdit, ExistingEditId: existingEdit.EditId);
        }

        async Task<FailedMessageEdit> ReloadConflictResult(string failedMessageId, CancellationToken cancellationToken)
        {
            using var reloadSession = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            return await reloadSession.LoadAsync<FailedMessageEdit>(FailedMessageEdit.MakeDocumentId(failedMessageId), cancellationToken);
        }

    }
}
