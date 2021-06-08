
namespace Particular.ServiceControl
{
    using Microsoft.Extensions.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    class HostingComponent : ServiceControlComponent
    {
        public override void Configure(Settings settings, IHostBuilder hostBuilder)
        {
        }

        public override void Setup(Settings settings, IComponentSetupContext context)
        {
            context.CreateQueue(settings.ServiceName);
        }
    }
}