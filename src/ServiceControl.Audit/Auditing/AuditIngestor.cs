namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using BodyStorage;
    using Infrastructure.Settings;
    using Monitoring;
    using NServiceBus;
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using Raven.Client;
    using Recoverability;
    using SagaAudit;
    using ServiceControl.Infrastructure.Metrics;

    class AuditIngestor
    {
        static readonly long FrequencyInMilliseconds = Stopwatch.Frequency / 1000;

        public AuditIngestor(
            Metrics metrics,
            Settings settings,
            IDocumentStore documentStore,
            IBodyStorage bodyStorage,
            EndpointInstanceMonitoring endpointInstanceMonitoring,
            IEnumerable<IEnrichImportedAuditMessages> auditEnrichers, // allows extending message enrichers with custom enrichers registered in the DI container
            IMessageSession messageSession
        )
        {
            var ingestedAuditMeter = metrics.GetCounter("Audit ingestion - ingested audit");
            var ingestedSagaAuditMeter = metrics.GetCounter("Audit ingestion - ingested saga audit");
            var auditBulkInsertDurationMeter = metrics.GetMeter("Audit ingestion - audit bulk insert duration", FrequencyInMilliseconds);
            var sagaAuditBulkInsertDurationMeter = metrics.GetMeter("Audit ingestion - saga audit bulk insert duration", FrequencyInMilliseconds);
            var bulkInsertCommitDurationMeter = metrics.GetMeter("Audit ingestion - bulk insert commit duration", FrequencyInMilliseconds);

            var enrichers = new IEnrichImportedAuditMessages[]
            {
                new MessageTypeEnricher(),
                new EnrichWithTrackingIds(),
                new ProcessingStatisticsEnricher(),
                new DetectNewEndpointsFromAuditImportsEnricher(endpointInstanceMonitoring),
                new DetectSuccessfulRetriesEnricher(),
                new SagaRelationshipsEnricher()
            }.Concat(auditEnrichers).ToArray();

            var bodyStorageEnricher = new BodyStorageEnricher(bodyStorage, settings);
            var auditPersister = new AuditPersister(documentStore, bodyStorageEnricher, enrichers, ingestedAuditMeter, ingestedSagaAuditMeter, auditBulkInsertDurationMeter, sagaAuditBulkInsertDurationMeter, bulkInsertCommitDurationMeter, messageSession);
            this.auditPersister = auditPersister;
            this.settings = settings;
        }

        public async Task Ingest(List<MessageContext> contexts, IDispatchMessages dispatcher)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug($"Ingesting {contexts.Count} message contexts");
            }

            var stored = await auditPersister.Persist(contexts, dispatcher).ConfigureAwait(false);

            try
            {
                if (settings.ForwardAuditMessages)
                {
                    if (log.IsDebugEnabled)
                    {
                        log.Debug($"Forwarding {contexts.Count} messages");
                    }
                    await Forward(stored, settings.AuditLogQueue, dispatcher)
                        .ConfigureAwait(false);
                    if (log.IsDebugEnabled)
                    {
                        log.Debug("Forwarded messages");
                    }
                }

                foreach (var context in stored)
                {
                    context.GetTaskCompletionSource().TrySetResult(true);
                }
            }
            catch (Exception e)
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug("Forwarding messages failed", e);
                }

                // making sure to rethrow so that all messages get marked as failed
                throw;
            }
        }

        Task Forward(IReadOnlyCollection<MessageContext> messageContexts, string forwardingAddress, IDispatchMessages dispatcher)
        {
            var transportOperations = new TransportOperation[messageContexts.Count]; //We could allocate based on the actual number of ProcessedMessages but this should be OK
            var index = 0;
            MessageContext anyContext = null;
            foreach (var messageContext in messageContexts)
            {
                if (messageContext.Extensions.TryGet("AuditType", out string auditType)
                    && auditType != "ProcessedMessage")
                {
                    continue;
                }

                anyContext = messageContext;
                var outgoingMessage = new OutgoingMessage(
                    messageContext.MessageId,
                    messageContext.Headers,
                    messageContext.Body);

                // Forwarded messages should last as long as possible
                outgoingMessage.Headers.Remove(Headers.TimeToBeReceived);

                transportOperations[index] = new TransportOperation(outgoingMessage, new UnicastAddressTag(forwardingAddress));
                index++;
            }

            return anyContext != null
                ? dispatcher.Dispatch(
                    new TransportOperations(transportOperations),
                    anyContext.TransportTransaction,
                    anyContext.Extensions
                )
                : Task.CompletedTask;
        }

        public async Task VerifyCanReachForwardingAddress(IDispatchMessages dispatcher)
        {
            if (!settings.ForwardAuditMessages)
            {
                return;
            }

            try
            {
                var transportOperations = new TransportOperations(
                    new TransportOperation(
                        new OutgoingMessage(Guid.Empty.ToString("N"),
                            new Dictionary<string, string>(),
                            new byte[0]),
                        new UnicastAddressTag(settings.AuditLogQueue)
                    )
                );

                await dispatcher.Dispatch(transportOperations, new TransportTransaction(), new ContextBag())
                    .ConfigureAwait(false);
            }
            catch (Exception e)
            {
                throw new Exception($"Unable to write to forwarding queue {settings.AuditLogQueue}", e);
            }
        }

        AuditPersister auditPersister;
        Settings settings;
        static ILog log = LogManager.GetLogger<AuditIngestor>();
    }
}