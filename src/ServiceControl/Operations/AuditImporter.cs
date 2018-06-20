namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Transport;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure;
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
            var settings = context.Settings.Get<Settings>();

            if(settings.IngestAuditMessages)
            {
                context.Container.ConfigureComponent<AuditImporter>(DependencyLifecycle.SingleInstance);
                context.Container.ConfigureComponent<AuditIngestor>(DependencyLifecycle.SingleInstance);

                SetupImportFailuresHandler(context);

                context.AddSatelliteReceiver(
                    "Audit Import",
                    context.Settings.ToTransportAddress(settings.AuditQueue), 
                    new PushRuntimeSettings(settings.MaximumConcurrencyLevel),
                    OnAuditError,
                    OnAuditMessage
                );
            }

            // TODO: Fail startup if can't write to audit forwarding queue but forwarding is enabled
        }

        private void SetupImportFailuresHandler(FeatureConfigurationContext context)
        {
            var store = context.Settings.Get<IDocumentStore>();
            var loggingSettings = context.Settings.Get<LoggingSettings>();

            importFailuresHandler = new SatelliteImportFailuresHandler(
                store, 
                Path.Combine(loggingSettings.LogPath, @"FailedImports\Audit"),
                msg => new FailedAuditImport
                {
                    Message = msg
                }, 
                // TODO: How do we get CriticalError?
                null
            );
        }

        private Task OnAuditMessage(IBuilder builder, MessageContext messageContext)
        {
            return builder.Build<AuditIngestor>().Ingest(messageContext);
        }

        private RecoverabilityAction OnAuditError(RecoverabilityConfig config, ErrorContext errorContext)
        {
            var recoverabilityAction = DefaultRecoverabilityPolicy.Invoke(config, errorContext);

            if (recoverabilityAction is MoveToError)
            {
                importFailuresHandler.Handle(errorContext).GetAwaiter().GetResult();
            }

            return recoverabilityAction;
        }

        private SatelliteImportFailuresHandler importFailuresHandler;
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

        public ProcessedMessage ConvertToSaveMessage(MessageContext message)
        {
            string messageId;
            if (!message.Headers.TryGetValue(Headers.MessageId, out messageId))
            {
                messageId = DeterministicGuid.MakeId(message.MessageId).ToString();
            }
            var metadata = new Dictionary<string, object>
            {
                ["MessageId"] = messageId,
                ["MessageIntent"] = message.Headers.MessageIntent(),
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