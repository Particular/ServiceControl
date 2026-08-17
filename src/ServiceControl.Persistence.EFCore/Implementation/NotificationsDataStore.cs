namespace ServiceControl.Persistence.EFCore.Implementation;

using Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Notifications;

public class NotificationsDataStore(IServiceScopeFactory scopeFactory) : DataStoreBase(scopeFactory), INotificationsDataStore
{
    public Task<NotificationsSettings> LoadSettings(CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (dbContext, ct) =>
        {
            var email = await dbContext.GetSetting<EmailNotifications>(SettingKeys.NotificationEmails, ct);
            return new NotificationsSettings { Email = email ?? new EmailNotifications() };
        }, cancellationToken);

    public Task SaveSettings(NotificationsSettings settings, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext((dbContext, ct) => dbContext.StoreSetting(SettingKeys.NotificationEmails, settings.Email, ct), cancellationToken);
}