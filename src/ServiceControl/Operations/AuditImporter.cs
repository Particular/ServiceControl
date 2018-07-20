namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using BodyStorage;
    using Infrastructure;
    using MessageAuditing;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Transport;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;

    public class AuditImporterFeature : Feature
    {
        public AuditImporterFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var settings = context.Settings.Get<Settings>("ServiceControl.Settings");

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
                    (builder, messageContext) => settings.OnMessage(messageContext.MessageId, messageContext.Headers, messageContext.Body, () => OnAuditMessage(builder, messageContext))
                );
            }

            // TODO: Fail startup if can't write to audit forwarding queue but forwarding is enabled
        }

        void SetupImportFailuresHandler(FeatureConfigurationContext context)
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

        Task OnAuditMessage(IBuilder builder, MessageContext messageContext)
        {
            return builder.Build<AuditIngestor>().Ingest(messageContext);
        }

        RecoverabilityAction OnAuditError(RecoverabilityConfig config, ErrorContext errorContext)
        {
            var recoverabilityAction = DefaultRecoverabilityPolicy.Invoke(config, errorContext);

            if (recoverabilityAction is MoveToError)
            {
                importFailuresHandler.Handle(errorContext).GetAwaiter().GetResult();
            }

            return recoverabilityAction;
        }

        SatelliteImportFailuresHandler importFailuresHandler;
    }


    public class AuditImporter
    {
        readonly IEnrichImportedMessages[] enrichers;
        readonly BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher;

        public AuditImporter(IBuilder builder, BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher)
        {
            this.bodyStorageEnricher = bodyStorageEnricher;
            enrichers = builder.BuildAll<IEnrichImportedMessages>().Where(e => e.EnrichAudits).ToArray();
        }

        public async Task<ProcessedMessage> ConvertToSaveMessage(MessageContext message)
        {
            if (!message.Headers.TryGetValue(Headers.MessageId, out var messageId))
            {
                messageId = DeterministicGuid.MakeId(message.MessageId).ToString();
            }
            var metadata = new Dictionary<string, object>
            {
                ["MessageId"] = messageId,
                ["MessageIntent"] = message.Headers.MessageIntent(),
                ["HeadersForSearching"] = string.Join(" ", message.Headers.Values)
            };

            // TODO: Fan out?
            foreach (var enricher in enrichers)
            {
                await enricher.Enrich(message.Headers, metadata)
                    .ConfigureAwait(false);
            }

            await bodyStorageEnricher.StoreAuditMessageBody(message.Body, message.Headers, metadata)
                .ConfigureAwait(false);

            var auditMessage = new ProcessedMessage(message.Headers, metadata)
            {
                // We do this so Raven does not spend time assigning a hilo key
                Id = $"ProcessedMessages/{Guid.NewGuid()}"
            };
            return auditMessage;
        }
    }
}