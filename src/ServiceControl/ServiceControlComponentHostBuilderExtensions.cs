namespace Particular.ServiceControl
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    static class ServiceControlComponentHostBuilderExtensions
    {
        public static IHostBuilder UseServiceControlComponents(this IHostBuilder hostBuilder, Settings settings, params ServiceControlComponent[] components)
        {
            var componentContext = new ComponentSetupContext();
            hostBuilder.ConfigureServices(services => services.AddSingleton(componentContext));
            foreach (var component in components)
            {
                // TODO: Fix this async call
                component.Setup(settings, componentContext).GetAwaiter().GetResult();
                component.Configure(settings, hostBuilder);
            }

            return hostBuilder;
        }
    }
}