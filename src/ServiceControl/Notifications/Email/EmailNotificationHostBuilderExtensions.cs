namespace ServiceControl.Notifications.Email
{
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    static class EmailNotificationHostBuilderExtensions
    {
        public static IHostBuilder UseEmailNotifications(this IHostBuilder hostBuilder)
        {
            hostBuilder.ConfigureServices(collection =>
            {
                collection.AddSingleton<EmailThrottlingState>();
                collection.AddDomainEventHandler<CustomChecksMailNotification>();
                collection.AddHostedService<EmailNotificationHostedService>();
            });
            return hostBuilder;
        }
    }
}