namespace ServiceControl.SagaAudit
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Operations;
    using Particular.ServiceControl;
    using ServiceBus.Management.Infrastructure.Settings;

    class SagaAuditComponent : ServiceControlComponent
    {
        public override void Configure(Settings settings, IHostBuilder hostBuilder)
        {
            hostBuilder.ConfigureServices(collection =>
            {
                collection.AddSingleton<IEnrichImportedErrorMessages, SagaRelationshipsEnricher>();
            });
        }

        public override void Setup(Settings settings, IComponentSetupContext context)
        {
            context.AddIndexAssembly(typeof(SagaSnapshot).Assembly);
        }

        internal class SagaRelationshipsEnricher : IEnrichImportedErrorMessages
        {
            public void Enrich(ErrorEnricherContext context) => InvokedSagasParser.Parse(context.Headers, context.Metadata);
        }
    }
}