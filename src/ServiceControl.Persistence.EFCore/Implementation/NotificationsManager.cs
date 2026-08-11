namespace ServiceControl.Persistence.EFCore.Implementation;

using DbContexts;
using Infrastructure;
using Notifications;

public class NotificationsManager(IAsyncDisposable scope, ServiceControlDbContext dbContext) : INotificationsManager
{
    NotificationsSettings? _settings;

    public Task SaveChanges(CancellationToken cancellationToken = default)
    {
        if (_settings == null)
        {
            return Task.CompletedTask;
        }

        return dbContext.StoreSetting(SettingKeys.NotificationEmails, _settings.Email, cancellationToken);
    }

    public async Task<NotificationsSettings> LoadSettings(CancellationToken cancellationToken = default)
    {
        var settings = await dbContext.GetSetting<EmailNotifications>(SettingKeys.NotificationEmails, cancellationToken);
        _settings = new NotificationsSettings() { Email = settings ?? new EmailNotifications() };
        return _settings;
    }

    public async ValueTask DisposeAsync()
    {
        await dbContext.DisposeAsync();
        await scope.DisposeAsync();

        GC.SuppressFinalize(this);
    }
}