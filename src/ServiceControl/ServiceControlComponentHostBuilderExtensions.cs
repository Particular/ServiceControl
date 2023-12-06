namespace Particular.ServiceControl
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    static class ServiceControlComponentHostBuilderExtensions
    {
        public static IHostApplicationBuilder UseServiceControlComponents(this IHostApplicationBuilder hostBuilder, Settings settings, params ServiceControlComponent[] components)
        {
            var componentContext = new ComponentInstallationContext();
            hostBuilder.Services.AddSingleton(componentContext);
            foreach (var component in components)
            {
                component.Setup(settings, componentContext);
                component.Configure(settings, hostBuilder);
            }

            return hostBuilder;
        }
    }
}