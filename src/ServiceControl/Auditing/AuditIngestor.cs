namespace ServiceControl.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.Ingestion;
    using ServiceControl.Operations;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.UnitOfWork;
    using ServiceControl.Transports;

    class AuditIngestor
    {
        public AuditIngestor(
            Settings settings,
            IIngestionUnitOfWorkFactory unitOfWorkFactory,
            IEndpointInstanceMonitoring endpointInstanceMonitoring,
            ITransportCustomization transportCustomization,
            ILogger<AuditIngestor> logger)
        {
            this.settings = settings;
            this.unitOfWorkFactory = unitOfWorkFactory;
            this.logger = logger;

            logQueueAddress = transportCustomization.ToTransportQualifiedQueueName(settings.AuditLogQueue);

            IEnrichImportedAuditMessages[] enrichers =
            [
                new AuditMessageTypeEnricher(),
                new AuditEnrichWithTrackingIds(),
                new AuditProcessingStatisticsEnricher(),
                new DetectNewEndpointsFromAuditImportsEnricher(endpointInstanceMonitoring),
                new DetectSuccessfulRetriesEnricher(),
                new SagaRelationshipsEnricher()
            ];

            processor = new AuditProcessor(enrichers, logger);
        }

        public async Task Ingest(List<MessageContext> contexts, IMessageDispatcher dispatcher, CancellationToken cancellationToken = default)
        {
            var stored = await Store(contexts, dispatcher, cancellationToken);

            try
            {
                if (settings.ForwardAuditMessages)
                {
                    await Forward(stored, logQueueAddress, dispatcher, cancellationToken);
                }

                foreach (var context in contexts)
                {
                    context.GetTaskCompletionSource().TrySetResult(true);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Forwarding messages failed");

                // making sure to rethrow so that all messages get marked as failed
                throw;
            }
        }

        async Task<IReadOnlyList<MessageContext>> Store(IReadOnlyList<MessageContext> contexts, IMessageDispatcher dispatcher, CancellationToken cancellationToken)
        {
            // deliberately not using the using statement because we dispose async explicitly
            IIngestionUnitOfWork unitOfWork = null;
            try
            {
                unitOfWork = await unitOfWorkFactory.StartNew(cancellationToken);

                var storedContexts = await processor.Process(contexts, unitOfWork, dispatcher, cancellationToken);

                await unitOfWork.Complete(cancellationToken);

                return storedContexts;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Bulk insertion failed");

                // making sure to rethrow so that all messages get marked as failed
                throw;
            }
            finally
            {
                if (unitOfWork != null)
                {
                    try
                    {
                        // this can throw even though dispose is never supposed to throw
                        await unitOfWork.DisposeAsync();
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning(e, "Bulk insertion dispose failed");

                        // making sure to rethrow so that all messages get marked as failed
                        throw;
                    }
                }
            }
        }

        static Task Forward(IReadOnlyCollection<MessageContext> messageContexts, string forwardingAddress, IMessageDispatcher dispatcher, CancellationToken cancellationToken)
        {
            var transportOperations = new List<TransportOperation>(messageContexts.Count);
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

                transportOperations.Add(new TransportOperation(outgoingMessage, new UnicastAddressTag(forwardingAddress)));
            }

            return anyContext != null
                ? dispatcher.Dispatch(new TransportOperations([.. transportOperations]), anyContext.TransportTransaction, cancellationToken)
                : Task.CompletedTask;
        }

        public async Task VerifyCanReachForwardingAddress(IMessageDispatcher dispatcher, CancellationToken cancellationToken = default)
        {
            if (!settings.ForwardAuditMessages)
            {
                return;
            }

            try
            {
                var transportOperations = new TransportOperations(
                    new TransportOperation(
                        new OutgoingMessage(Guid.Empty.ToString("N"), [], Array.Empty<byte>()),
                        new UnicastAddressTag(logQueueAddress)));

                await dispatcher.Dispatch(transportOperations, new TransportTransaction(), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new Exception($"Unable to write to forwarding queue {settings.AuditLogQueue}", e);
            }
        }

        readonly AuditProcessor processor;
        readonly IIngestionUnitOfWorkFactory unitOfWorkFactory;
        readonly Settings settings;
        readonly string logQueueAddress;
        readonly ILogger<AuditIngestor> logger;
    }
}
