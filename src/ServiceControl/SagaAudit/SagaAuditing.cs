namespace ServiceControl.SagaAudit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Features;
    using Operations;

    public class SagaAuditing : Feature
    {
        public SagaAuditing() => EnableByDefault();

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<SagaRelationshipsEnricher>(DependencyLifecycle.SingleInstance);
        }

        internal class SagaRelationshipsEnricher : IEnrichImportedErrorMessages
        {
            public void Enrich(ErrorEnricherContext context) => InvokedSagasParser.Parse(context.Headers, context.Metadata);
        }
    }
}