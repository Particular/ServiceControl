namespace ServiceControl.ExternalIntegrations
{
    using Microsoft.Extensions.DependencyInjection;

    public static class ExternalIntegrationsServiceCollectionExtensions
    {
        public static void AddIntegrationEventPublisher<T>(this IServiceCollection serviceCollection)
            where T : class, IEventPublisher
        {
            serviceCollection.AddSingleton<IEventPublisher, T>();
        }
    }
}