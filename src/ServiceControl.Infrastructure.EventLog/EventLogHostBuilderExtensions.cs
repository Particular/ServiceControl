namespace ServiceControl.EventLog
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Infrastructure.DomainEvents;

    public static class EventLogHostBuilderExtensions
    {
        public static IHostBuilder UseEventLog(this IHostBuilder hostBuilder)
        {
            hostBuilder.ConfigureServices(collection =>
            {
                collection.AddSingleton<EventLogMappings>();
                collection.AddDomainEventHandler<AuditEventLogWriter>();
            });
            return hostBuilder;
        }
    }
}