namespace ServiceControl.EndpointControl.Handlers
{
    using System.Collections.Generic;
    using NServiceBus;
    using NServiceBus.Features;
    using Operations;
    using Particular.Operations.Audits.Api;
    using ServiceControl.Contracts.Operations;

    public class AuditProcessorExecutorFeature : Feature
    {
        public AuditProcessorExecutorFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<InvokeProcessorsEnricher>(DependencyLifecycle.SingleInstance);
        }

        class InvokeProcessorsEnricher : IEnrichImportedMessages
        {
            IEnumerable<IProcessAudits> auditProcessors;

            public InvokeProcessorsEnricher(IEnumerable<IProcessAudits> auditProcessors)
            {
                this.auditProcessors = auditProcessors;
            }

            public void Enrich(IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata)
            {
                var sendingEndpoint = EndpointDetailsParser.SendingEndpoint(headers);
                var receivingEndpoint = EndpointDetailsParser.ReceivingEndpoint(headers);

                var auditMessage = new AuditMessage(headers, sendingEndpoint, receivingEndpoint);

                foreach (var processor in auditProcessors)
                {
                    processor.Handle(auditMessage).GetAwaiter().GetResult();
                }
            }

            public bool EnrichErrors => false;
            public bool EnrichAudits => true;
        }
    }
}