namespace ServiceControl.SagaAudit
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Particular.ServiceControl;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.CustomChecks;

    class SagaAuditComponent : ServiceControlComponent
    {
        public override void Configure(Settings settings, IHostBuilder hostBuilder)
        {
            // Forward saga audit messages and warn in ServiceControl 5, remove in 6
            hostBuilder.ConfigureServices(collection =>
            {
                collection.AddCustomCheck<SagaAuditDestinationCustomCheck>();
                collection.AddSingleton<SagaAuditDestinationCustomCheck.State>();
            });
        }

        public override void Setup(Settings settings, IComponentInstallationContext context) { }
    }
}