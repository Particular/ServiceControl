namespace ServiceControl.Infrastructure.DomainEvents
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using SignalR;

    static class ServicePulseNotifierHostBuilderExtensions
    {
        public static IHostBuilder UseServicePulseSignalRNotifier(this IHostBuilder hostBuilder)
        {
            hostBuilder.ConfigureServices(collection =>
            {
                collection.AddDomainEventHandler<ServicePulseNotifier>();
                collection.AddSingleton<GlobalEventHandler>();
            });
            return hostBuilder;
        }
    }
}