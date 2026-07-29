namespace ServiceControl.Persistence.EFCore.Implementation;

public class NotificationsDataStore : INotificationsDataStore
{
    public Task<INotificationsManager> CreateNotificationsManager() =>
        throw new NotImplementedException();
}
