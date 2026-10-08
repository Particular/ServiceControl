namespace ServiceControl.ExternalIntegrations
{
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Particular.ServiceControl;
    using ServiceBus.Management.Infrastructure.Settings;
    using Transports;

    class ExternalIntegrationsComponent : ServiceControlComponent
    {
        public override void Configure(Settings settings, ITransportCustomization transportCustomization, IHostApplicationBuilder hostBuilder)
        {
            var services = hostBuilder.Services;
            services.AddEventLogMapping<ExternalIntegrationEventFailedToBePublishedDefinition>();

            // Webhook notifications are fed from the same outbox as the integration events published on the bus
            if (!settings.DisableExternalIntegrationsPublishing || settings.Webhooks.Length > 0)
            {
                services.AddDomainEventHandler<IntegrationEventWriter>();

                if (!settings.ErrorIngestionOnly)
                {
                    services.AddHostedService<EventDispatcherHostedService>();
                }
            }
        }
    }
}