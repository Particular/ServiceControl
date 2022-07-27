
namespace Particular.ServiceControl
{
    using Microsoft.Extensions.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    abstract class ServiceControlComponent
    {
        public abstract void Configure(Settings settings, IHostBuilder hostBuilder);

        public virtual void Setup(Settings settings, IComponentInstallationContext context)
        {
        }
    }
}