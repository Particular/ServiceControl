namespace ServiceControl.Notifications.Email
{
    using Api;
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    public static class EmailNotificationHostBuilderExtensions
    {
        public static IHostBuilder UseEmailNotifications(this IHostBuilder hostBuilder, string instanceName, string apiUrl, string filter, string dropFolder)
        {
            hostBuilder.ConfigureServices(collection =>
            {
                collection.AddSingleton<EmailThrottlingState>();
                collection.AddSingleton(new NotificationsAppSettings(instanceName, apiUrl, filter, dropFolder));
                collection.AddDomainEventHandler<CustomChecksMailNotification>();
                collection.AddHostedService<EmailNotificationHostedService>();
            });
            return hostBuilder;
        }
    }
}