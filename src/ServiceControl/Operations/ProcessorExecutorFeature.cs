namespace ServiceControl.EndpointControl.Handlers
{
    using System.Collections.Generic;
    using NServiceBus;
    using NServiceBus.Features;
    using Operations;
    using Particular.Operations.Audits.Api;
    using Particular.Operations.Errors.Api;
    using ServiceControl.Contracts.Operations;

    public class ProcessorExecutorFeature : Feature
    {
        public ProcessorExecutorFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<InvokeAuditProcessorsEnricher>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<InvokeErrorProcessorsEnricher>(DependencyLifecycle.SingleInstance);
        }

        class InvokeErrorProcessorsEnricher : IEnrichImportedMessages
        {
            IEnumerable<IProcessErrors> errorProcessors;

            public InvokeErrorProcessorsEnricher(IEnumerable<IProcessErrors> errorProcessors)
            {
                this.errorProcessors = errorProcessors;
            }

            public void Enrich(IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata)
            {
                var sendingEndpoint = EndpointDetailsParser.SendingEndpoint(headers);
                var receivingEndpoint = EndpointDetailsParser.ReceivingEndpoint(headers);

                var errorMessage = new ErrorMessage(headers, sendingEndpoint, receivingEndpoint);

                foreach (var processor in errorProcessors)
                {
                    processor.Handle(errorMessage).GetAwaiter().GetResult();
                }
            }

            public bool EnrichErrors => true;
            public bool EnrichAudits => false;
        }

        class InvokeAuditProcessorsEnricher : IEnrichImportedMessages
        {
            IEnumerable<IProcessAudits> auditProcessors;

            public InvokeAuditProcessorsEnricher(IEnumerable<IProcessAudits> auditProcessors)
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