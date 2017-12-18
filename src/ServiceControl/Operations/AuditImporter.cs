namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.ObjectBuilder;
    using ServiceControl.MessageAuditing;
    using ServiceControl.Operations.BodyStorage;

    public class AuditImporterFeature : Feature
    {
        public AuditImporterFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<AuditImporter>(DependencyLifecycle.SingleInstance);
        }
    }

    public class AuditImporter
    {
        private readonly IEnrichImportedMessages[] enrichers;
        private readonly BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher;

        public AuditImporter(IBuilder builder, BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher)
        {
            this.bodyStorageEnricher = bodyStorageEnricher;
            enrichers = builder.BuildAll<IEnrichImportedMessages>().Where(e => e.EnrichAudits).ToArray();
        }

        public ProcessedMessage ConvertToSaveMessage(TransportMessage message)
        {
            var metadata = new Dictionary<string, object>
            {
                ["MessageId"] = message.Id,
                ["MessageIntent"] = message.MessageIntent,
                ["HeadersForSearching"] = string.Join(" ", message.Headers.Values)
            };

            foreach (var enricher in enrichers)
            {
                enricher.Enrich(message.Headers, metadata);
            }

            bodyStorageEnricher.StoreAuditMessageBody(
                message.Body,
                message.Headers,
                metadata);

            var auditMessage = new ProcessedMessage(message.Headers, metadata)
            {
                // We do this so Raven does not spend time assigning a hilo key
                Id = $"ProcessedMessages/{Guid.NewGuid()}"
            };
            return auditMessage;
        }
    }
}