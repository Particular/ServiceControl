namespace Particular.ServiceControl
{
    using global::ServiceControl.Transports;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Settings;

    static class ServiceControlComponentHostBuilderExtensions
    {
        public static void AddServiceControlComponents(this IHostApplicationBuilder hostBuilder, ComponentInstallationContext componentContext, Settings settings, EndpointConfiguration endpointConfiguration,
            ITransportCustomization transportCustomization,
            params ServiceControlComponent[] components)
        {
            hostBuilder.Services.AddSingleton(componentContext);
            foreach (var component in components)
            {
                component.Configure(settings, endpointConfiguration, transportCustomization, hostBuilder);
            }
        }
    }
}