﻿namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Infrastructure.Settings;
    using Monitoring;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using Persistence.UnitOfWork;
    using Recoverability;
    using SagaAudit;
    using ServiceControl.Infrastructure.Metrics;
    using ServiceControl.Transports;

    public class AuditIngestor
    {
        public AuditIngestor(
            Metrics metrics,
            Settings settings,
            IAuditIngestionUnitOfWorkFactory unitOfWorkFactory,
            EndpointInstanceMonitoring endpointInstanceMonitoring,
            IEnumerable<IEnrichImportedAuditMessages> auditEnrichers, // allows extending message enrichers with custom enrichers registered in the DI container
            IMessageSession messageSession,
            Lazy<IMessageDispatcher> messageDispatcher,
            ITransportCustomization transportCustomization
        )
        {
            this.settings = settings;
            this.messageDispatcher = messageDispatcher;

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

            logQueueAddress = transportCustomization.ToTransportQualifiedQueueName(settings.AuditLogQueue);

            auditPersister = new AuditPersister(unitOfWorkFactory, enrichers, ingestedAuditMeter, ingestedSagaAuditMeter, auditBulkInsertDurationMeter, sagaAuditBulkInsertDurationMeter, bulkInsertCommitDurationMeter, messageSession, messageDispatcher);
        }

        public async Task Ingest(List<MessageContext> contexts)
        {
            if (Log.IsDebugEnabled)
            {
                Log.Debug($"Ingesting {contexts.Count} message contexts");
            }

            var stored = await auditPersister.Persist(contexts);

            try
            {
                if (settings.ForwardAuditMessages)
                {
                    if (Log.IsDebugEnabled)
                    {
                        Log.Debug($"Forwarding {stored.Count} messages");
                    }
                    await Forward(stored, logQueueAddress);
                    if (Log.IsDebugEnabled)
                    {
                        Log.Debug("Forwarded messages");
                    }
                }

                foreach (var context in contexts)
                {
                    context.GetTaskCompletionSource().TrySetResult(true);
                }
            }
            catch (Exception e)
            {
                if (Log.IsWarnEnabled)
                {
                    Log.Warn("Forwarding messages failed", e);
                }

                // making sure to rethrow so that all messages get marked as failed
                throw;
            }
        }

        Task Forward(IReadOnlyCollection<MessageContext> messageContexts, string forwardingAddress)
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
                    messageContext.NativeMessageId,
                    messageContext.Headers,
                    messageContext.Body);

                // Forwarded messages should last as long as possible
                outgoingMessage.Headers.Remove(Headers.TimeToBeReceived);

                transportOperations[index] = new TransportOperation(outgoingMessage, new UnicastAddressTag(forwardingAddress));
                index++;
            }

            return anyContext != null
                ? messageDispatcher.Value.Dispatch(
                    new TransportOperations(transportOperations),
                    anyContext.TransportTransaction
                )
                : Task.CompletedTask;
        }

        public async Task VerifyCanReachForwardingAddress()
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
                            [], Array.Empty<byte>()),
                        new UnicastAddressTag(logQueueAddress)
                    )
                );

                await messageDispatcher.Value.Dispatch(transportOperations, new TransportTransaction());
            }
            catch (Exception e)
            {
                throw new Exception($"Unable to write to forwarding queue {settings.AuditLogQueue}", e);
            }
        }

        readonly AuditPersister auditPersister;
        readonly Settings settings;
        readonly Lazy<IMessageDispatcher> messageDispatcher;
        readonly string logQueueAddress;

        static readonly long FrequencyInMilliseconds = Stopwatch.Frequency / 1000;
        static readonly ILog Log = LogManager.GetLogger<AuditIngestor>();
    }
}