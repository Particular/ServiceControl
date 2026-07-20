namespace ServiceControl.Persistence.EFCore.Implementation;

using ServiceControl.MessageFailures;

public class EditFailedMessagesManager : IEditFailedMessagesManager
{
    public Task<FailedMessage> GetFailedMessage(string failedMessageId) =>
        throw new NotImplementedException();

    public Task<string> GetCurrentEditingRequestId(string failedMessageId) =>
        throw new NotImplementedException();

    public Task SetCurrentEditingRequestId(string editingMessageId) =>
        throw new NotImplementedException();

    public Task SetFailedMessageAsResolved() =>
        throw new NotImplementedException();

    public Task SaveChanges() =>
        throw new NotImplementedException();

    public void Dispose()
    {
        // Nothing to dispose yet
        GC.SuppressFinalize(this);
    }
}
