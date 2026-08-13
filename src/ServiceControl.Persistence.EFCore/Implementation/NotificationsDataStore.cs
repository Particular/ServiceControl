namespace ServiceControl.Persistence.EFCore.Implementation;

using DbContexts;
using Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Notifications;

public class NotificationsDataStore(IServiceScopeFactory scopeFactory) : INotificationsDataStore
{
    public async Task<NotificationsSettings> LoadSettings(CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
        var email = await dbContext.GetSetting<EmailNotifications>(SettingKeys.NotificationEmails, cancellationToken);

        return new NotificationsSettings { Email = email ?? new EmailNotifications() };
    }

    public async Task SaveSettings(NotificationsSettings settings, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
        await dbContext.StoreSetting(SettingKeys.NotificationEmails, settings.Email, cancellationToken);
    }
}
