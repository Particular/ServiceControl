namespace ServiceControl.Persistence.EFCore.Implementation;

using ServiceControl.Notifications;

public class NotificationsManager : INotificationsManager
{
    public Task<NotificationsSettings> LoadSettings() =>
        throw new NotImplementedException();

    public Task SaveChanges() =>
        throw new NotImplementedException();

    public ValueTask DisposeAsync() => default;
}
