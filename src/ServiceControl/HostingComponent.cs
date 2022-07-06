
namespace Particular.ServiceControl
{
    using System.Threading.Tasks;
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
                services.AddSingleton<IPlatformConnectionBuilder, PlatformConnectionBuilder>();
            });

        public override Task Setup(Settings settings, IComponentSetupContext context)
        {
            context.CreateQueue(settings.ServiceName);
            return Task.CompletedTask;
        }
    }
}