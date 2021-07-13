namespace ServiceControl.SagaAudit
{
    using Microsoft.Extensions.Hosting;
    using Particular.ServiceControl;
    using ServiceBus.Management.Infrastructure.Settings;

    class SagaAuditComponent : ServiceControlComponent
    {
        public override void Configure(Settings settings, IHostBuilder hostBuilder)
        {
        }

        public override void Setup(Settings settings, IComponentSetupContext context)
        {
            context.AddIndexAssembly(typeof(SagaSnapshot).Assembly);
        }
    }
}