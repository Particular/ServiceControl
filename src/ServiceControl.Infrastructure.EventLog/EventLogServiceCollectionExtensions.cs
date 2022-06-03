namespace ServiceControl.ExternalIntegrations
{
    using EventLog;
    using Microsoft.Extensions.DependencyInjection;

    public static class EventLogServiceCollectionExtensions
    {
        public static void AddEventLogMapping<T>(this IServiceCollection serviceCollection)
            where T : class, IEventLogMappingDefinition
        {
            serviceCollection.AddSingleton<IEventLogMappingDefinition, T>();
        }
    }
}