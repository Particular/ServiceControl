namespace ServiceControl.SagaAudit
{
    using Microsoft.Extensions.Hosting;
    using Particular.ServiceControl;
    using ServiceBus.Management.Infrastructure.Settings;

    class SagaAuditComponent : ServiceControlComponent
    {
        public override void Configure(Settings settings, IHostBuilder hostBuilder)
        {
            // TODO: If this component doesn't do anything, should it even exist?
        }

        public override void Setup(Settings settings, IComponentInstallationContext context)
        {
        }
    }
}