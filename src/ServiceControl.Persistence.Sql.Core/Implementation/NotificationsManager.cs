namespace ServiceControl.Persistence.Sql.Core.Implementation;

using System;
using System.Text.Json;
using System.Threading.Tasks;
using DbContexts;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Notifications;
using ServiceControl.Persistence;

class NotificationsManager(IServiceScope scope) : INotificationsManager
{
    readonly ServiceControlDbContextBase dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContextBase>();

    public async Task<NotificationsSettings> LoadSettings(TimeSpan? cacheTimeout = null)
    {
        var entity = await dbContext.NotificationsSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == Guid.Parse(NotificationsSettings.SingleDocumentId));

        if (entity == null)
        {
            // Return default settings if none exist
            return new NotificationsSettings
            {
                Id = NotificationsSettings.SingleDocumentId,
                Email = new EmailNotifications()
            };
        }

        var emailSettings = JsonSerializer.Deserialize<EmailNotifications>(entity.EmailSettingsJson, JsonSerializationOptions.Default) ?? new EmailNotifications();

        return new NotificationsSettings
        {
            Id = entity.Id.ToString(),
            Email = emailSettings
        };
    }

    public async Task<int> GetUnresolvedCount()
    {
        return await dbContext.FailedMessages
            .AsNoTracking()
            .Where(m => m.Status == ServiceControl.MessageFailures.FailedMessageStatus.Unresolved)
            .CountAsync();
    }

    public async Task SaveChanges()
    {
        await dbContext.SaveChangesAsync();
    }

    public void Dispose()
    {
        scope.Dispose();
    }
}
