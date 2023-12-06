namespace ServiceControl.ExternalIntegrations
{
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    static class ExternalIntegrationsHostBuilderExtensions
    {
        public static IHostApplicationBuilder UseExternalIntegrationEvents(this IHostApplicationBuilder hostBuilder)
        {
            var services = hostBuilder.Services;
            services.AddHostedService<EventDispatcherHostedService>();
            services.AddDomainEventHandler<IntegrationEventWriter>();
            return hostBuilder;
        }
    }
}