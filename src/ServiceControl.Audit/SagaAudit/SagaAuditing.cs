namespace ServiceControl.Audit.SagaAudit
{
    using Auditing;
    using NServiceBus;
    using NServiceBus.Features;
    using ServiceControl.SagaAudit;

    class SagaAuditing : Feature
    {
        public SagaAuditing()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<SagaRelationshipsEnricher>(DependencyLifecycle.SingleInstance);
        }

        internal class SagaRelationshipsEnricher : IEnrichImportedAuditMessages
        {
            public void Enrich(AuditEnricherContext context)
            {
                var headers = context.Headers;
                var metadata = context.Metadata;

                InvokedSagasParser.Parse(headers, metadata);
            }
        }
    }
}