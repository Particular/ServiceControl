namespace ServiceControl.SagaAudit
{
    using Microsoft.Extensions.Hosting;
    using Particular.ServiceControl;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.ExternalIntegrations;

    class SagaAuditComponent : ServiceControlComponent
    {
        public override void Configure(Settings settings, IHostBuilder hostBuilder)
        {
            // Forward saga audit messages and warn in ServiceControl 5, remove in 6
            hostBuilder.ConfigureServices(collection =>
            {
                collection.AddEventLogMapping<EndpointReportingSagaAuditToPrimaryDefinition>();
            });
        }

        public override void Setup(Settings settings, IComponentInstallationContext context)
        {
        }
    }
}