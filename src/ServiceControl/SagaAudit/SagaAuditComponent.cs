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
            // THEORY: Remove in V5, since then there will be no audit capabilities left in the primary instance
        }

        public override void Setup(Settings settings, IComponentInstallationContext context)
        {
        }
    }
}