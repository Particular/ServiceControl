namespace ServiceControl.SagaAudit
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Operations;

    static class SagaAuditingHostBuilderExtensions
    {
        public static IHostBuilder UseSagaAudit(this IHostBuilder hostBuilder)
        {
            hostBuilder.ConfigureServices(collection =>
            {
                collection.AddSingleton<SagaRelationshipsEnricher>();
            });
            return hostBuilder;
        }
        internal class SagaRelationshipsEnricher : IEnrichImportedErrorMessages
        {
            public void Enrich(ErrorEnricherContext context) => InvokedSagasParser.Parse(context.Headers, context.Metadata);
        }
    }
}