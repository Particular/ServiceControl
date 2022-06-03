namespace ServiceControl.ExternalIntegrations
{
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using Raven.Client;

    public static class ExternalIntegrationsHostBuilderExtensions
    {
        public static IHostBuilder UseExternalIntegrationEvents(this IHostBuilder hostBuilder, int batchSize)
        {
            hostBuilder.ConfigureServices(collection =>
            {
                collection.AddEventLogMapping<ExternalIntegrationEventFailedToBePublishedDefinition>();
                collection.AddHostedService(
                    sp => new EventDispatcherHostedService(
                        sp.GetRequiredService<IDocumentStore>(),
                        sp.GetRequiredService<IDomainEvents>(),
                        sp.GetRequiredService<CriticalError>(),
                        batchSize,
                        sp.GetServices<IEventPublisher>(),
                        sp.GetRequiredService<IMessageSession>()));
                collection.AddDomainEventHandler<IntegrationEventWriter>();
            });
            return hostBuilder;
        }
    }
}