namespace ServiceControl.ExternalIntegrations
{
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    static class PersistenceHostBuilderExtensions
    {
        public static IHostBuilder UseExternalIntegrationEvents(this IHostBuilder hostBuilder)
        {
            hostBuilder.ConfigureServices(collection =>
            {
                collection.AddHostedService<EventDispatcherHostedService>();
                collection.AddDomainEventHandler<IntegrationEventWriter>();
            });
            return hostBuilder;
        }
    }
}