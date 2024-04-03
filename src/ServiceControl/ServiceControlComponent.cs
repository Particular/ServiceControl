
namespace Particular.ServiceControl
{
    using global::ServiceControl.Transports;
    using Microsoft.Extensions.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;

    abstract class ServiceControlComponent
    {
        public abstract void Configure(Settings settings, ITransportCustomization transportCustomization,
            IHostApplicationBuilder hostBuilder);

        public virtual void ConfigureInstallation(Settings settings, IHostApplicationBuilder hostBuilder)
        {
        }

        public virtual void Setup(Settings settings, IComponentInstallationContext context)
        {
        }
    }
}