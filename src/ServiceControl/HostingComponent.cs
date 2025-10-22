
namespace Particular.ServiceControl
{
    using global::ServiceControl.Connection;
    using global::ServiceControl.Transports;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    class HostingComponent : ServiceControlComponent
    {
        public override void Configure(Settings settings, ITransportCustomization transportCustomization, IHostApplicationBuilder hostBuilder)
        {
            var services = hostBuilder.Services;
            services.AddPlatformConnectionProvider<RemotePlatformConnectionDetailsProvider>();
            services.AddSingleton<IPlatformConnectionBuilder, PlatformConnectionBuilder>();
        }

        public override void Setup(Settings settings, IComponentInstallationContext context, IHostApplicationBuilder hostBuilder) => context.CreateQueue(settings.ServiceControl.InstanceName);
    }
}