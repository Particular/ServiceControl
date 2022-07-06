namespace ServiceControl.SagaAudit
{
    using System.Threading.Tasks;
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

        public override Task Setup(Settings settings, IComponentSetupContext context)
        {
            context.AddIndexAssembly(typeof(SagaSnapshot).Assembly);
            return Task.CompletedTask;
        }
    }
}