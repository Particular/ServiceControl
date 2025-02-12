namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
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
    using ServiceControl.Transports;

    public class AuditIngestor
    {
        public AuditIngestor(
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
            var enrichers = new IEnrichImportedAuditMessages[] { new MessageTypeEnricher(), new EnrichWithTrackingIds(), new ProcessingStatisticsEnricher(), new DetectNewEndpointsFromAuditImportsEnricher(endpointInstanceMonitoring), new DetectSuccessfulRetriesEnricher(), new SagaRelationshipsEnricher() }.Concat(auditEnrichers).ToArray();

            logQueueAddress = transportCustomization.ToTransportQualifiedQueueName(settings.AuditLogQueue);

            auditPersister = new AuditPersister(
                unitOfWorkFactory,
                enrichers,
                messageSession,
                messageDispatcher
            );
        }

        public async Task Ingest(List<MessageContext> contexts)
        {
            var stored = await auditPersister.Persist(contexts);

            try
            {
                if (settings.ForwardAuditMessages)
                {
                    await Forward(stored, logQueueAddress);
                    forwardedMessagesCounter.Add(stored.Count);
                }

                foreach (var context in contexts)
                {
                    context.GetTaskCompletionSource().TrySetResult(true);
                }
            }
            catch (Exception e)
            {
                Log.Warn("Forwarding messages failed", e);

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
        readonly Counter<long> forwardedMessagesCounter = Telemetry.Meter.CreateCounter<long>(Telemetry.CreateInstrumentName("ingestion", "forwarded"), description: "Audit ingestion forwarded message count");

        static readonly ILog Log = LogManager.GetLogger<AuditIngestor>();
    }
}