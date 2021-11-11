namespace ServiceControl.SagaAudit
{
    using CustomChecks;
    using Microsoft.Extensions.Hosting;
    using Particular.ServiceControl;
    using ServiceBus.Management.Infrastructure.Settings;

    class SagaAuditComponent : ServiceControlComponent
    {
        public override void Configure(Settings settings, IHostBuilder hostBuilder)
        {
            hostBuilder.ConfigureServices(collection =>
            {
                collection.AddCustomCheck<AuditRetentionCustomCheck>();
            });
        }

        public override void Setup(Settings settings, IComponentSetupContext context)
        {
            context.AddIndexAssembly(typeof(SagaSnapshot).Assembly);
        }
    }
}