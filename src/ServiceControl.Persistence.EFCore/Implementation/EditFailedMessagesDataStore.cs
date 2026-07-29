namespace ServiceControl.Persistence.EFCore.Implementation;

public class EditFailedMessagesDataStore : IEditFailedMessagesDataStore
{
    public Task<IEditFailedMessagesManager> CreateEditFailedMessageManager() =>
        throw new NotImplementedException();
}
