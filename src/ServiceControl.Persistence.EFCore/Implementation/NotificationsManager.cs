namespace ServiceControl.Persistence.EFCore.Implementation;

using ServiceControl.Notifications;

public class NotificationsManager : INotificationsManager
{
    public Task<NotificationsSettings> LoadSettings(TimeSpan? cacheTimeout = null) =>
        throw new NotImplementedException();

    public Task SaveChanges() =>
        throw new NotImplementedException();

    public void Dispose()
    {
        // Nothing to dispose yet
        GC.SuppressFinalize(this);
    }
}
