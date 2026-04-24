namespace Particular.ServiceControl
{
    using global::ServiceControl.Transports;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Settings;

    abstract class ServiceControlComponent
    {
        public abstract void Configure(Settings settings, EndpointConfiguration endpointConfiguration, ITransportCustomization transportCustomization, IHostApplicationBuilder hostBuilder);

        public virtual void Setup(Settings settings, IComponentInstallationContext context, IHostApplicationBuilder hostBuilder)
        {
        }
    }
}