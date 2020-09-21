using System.Collections.Generic;

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

        class SagaRelationshipsEnricher : IEnrichImportedAuditMessages
        {
            public void Enrich(AuditEnricherContext context)
            {
                var result = InvokedSagasParser.Parse(context.Headers);

                context.Metadata.InvokedSagas = result.InvokedSagas;
                context.Metadata.OriginatesFromSaga = result.OriginatesFromSaga;
            }
        }
    }
}