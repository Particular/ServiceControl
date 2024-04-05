namespace Particular.ServiceControl
{
    using global::ServiceControl.Transports;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    static class ServiceControlComponentHostBuilderExtensions
    {
        public static void AddServiceControlComponents(this IHostApplicationBuilder hostBuilder, Settings settings,
            ITransportCustomization transportCustomization,
            params ServiceControlComponent[] components)
        {
            var componentContext = new ComponentInstallationContext();
            hostBuilder.Services.AddSingleton(componentContext);
            foreach (var component in components)
            {
                component.Setup(settings, componentContext, hostBuilder);
                component.Configure(settings, transportCustomization, hostBuilder);
            }
        }
    }
}