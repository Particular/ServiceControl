namespace ServiceControl.Notifications.Email
{
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    static class EmailNotificationHostBuilderExtensions
    {
        public static IHostApplicationBuilder AddEmailNotifications(this IHostApplicationBuilder hostBuilder)
        {
            var services = hostBuilder.Services;
            services.AddSingleton<EmailThrottlingState>();
            services.AddSingleton<EmailSender>();
            services.AddDomainEventHandler<CustomChecksMailNotification>();
            services.AddHostedService<EmailNotificationHostedService>();
            return hostBuilder;
        }
    }
}