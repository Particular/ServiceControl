namespace ServiceControl.Notifications.Webhooks
{
    using System.Threading;
    using ExternalIntegrations;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    static class WebhookNotificationHostBuilderExtensions
    {
        public static IHostApplicationBuilder AddWebhookNotifications(this IHostApplicationBuilder hostBuilder, Settings settings)
        {
            var services = hostBuilder.Services;

            // Always registered so that already queued notifications can be handled after webhooks have been removed from the configuration
            services.TryAddSingleton(new WebhookDeliveryOptions());
            services.AddSingleton<AlertmanagerAlertFactory>();
            services.AddSingleton<WebhookSender>();
            services.AddEventLogMapping<WebhookNotificationFailedDefinition>();
            services.AddHttpClient(WebhookSender.HttpClientName, client => client.Timeout = Timeout.InfiniteTimeSpan) // timeouts are applied per attempt by the resilience pipeline
                .RemoveAllLoggers(); // the default HttpClient logging includes the request URL, which can contain secrets

            if (settings.Webhooks.Length > 0)
            {
                services.AddSingleton<IIntegrationEventSink, WebhookIntegrationEventSink>();
            }

            return hostBuilder;
        }
    }
}
