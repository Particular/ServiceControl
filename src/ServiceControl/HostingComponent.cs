
namespace Particular.ServiceControl
{
    using global::ServiceControl.Connection;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    class HostingComponent : ServiceControlComponent
    {
        public override void Configure(Settings settings, IHostBuilder hostBuilder) =>
            hostBuilder.ConfigureServices(services =>
            {
                services.AddPlatformConnectionProvider<RemotePlatformConnectionDetailsProvider>();
                services.AddTransient<IPlatformConnectionBuilder, PlatformConnectionBuilder>();
            });

        public override void Setup(Settings settings, IComponentSetupContext context)
        {
            context.CreateQueue(settings.ServiceName);
        }
    }
}