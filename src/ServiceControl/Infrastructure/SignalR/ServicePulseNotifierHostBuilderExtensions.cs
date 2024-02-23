namespace ServiceControl.Infrastructure.DomainEvents
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using SignalR;

    static class ServicePulseNotifierHostBuilderExtensions
    {
        public static IHostApplicationBuilder AddServicePulseSignalRNotifier(this IHostApplicationBuilder hostBuilder)
        {
            var services = hostBuilder.Services;
            services.AddDomainEventHandler<ServicePulseNotifier>();
            services.AddSingleton<GlobalEventHandler>();
            return hostBuilder;
        }
    }
}