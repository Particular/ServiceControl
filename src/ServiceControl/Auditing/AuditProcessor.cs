namespace ServiceControl.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using NServiceBus.Transport;
    using ServiceControl.EndpointPlugin.Messages.SagaState;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageAuditing;
    using ServiceControl.Infrastructure.Ingestion;
    using ServiceControl.Operations;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.UnitOfWork;
    using ServiceControl.SagaAudit;

    class AuditProcessor(IEnrichImportedAuditMessages[] enrichers, ILogger logger)
    {
        public async Task<IReadOnlyList<MessageContext>> Process(IReadOnlyList<MessageContext> contexts, IIngestionUnitOfWork unitOfWork, IMessageDispatcher dispatcher, CancellationToken cancellationToken = default)
        {
            var audit = unitOfWork.Audit
                ?? throw new InvalidOperationException("The configured persistence does not support audit ingestion.");
            var monitoring = unitOfWork.Monitoring
                ?? throw new InvalidOperationException("The configured persistence does not support monitoring.");

            var storedContexts = new List<MessageContext>(contexts.Count);

            var tasks = new List<Task>(contexts.Count);
            foreach (var context in contexts)
            {
                tasks.Add(ProcessMessage(context, dispatcher, cancellationToken));
            }

            await Task.WhenAll(tasks);

            var knownEndpoints = new Dictionary<string, KnownEndpoint>();

            foreach (var context in contexts)
            {
                // Any message context that failed during processing will have a faulted task and should be skipped
                if (context.GetTaskCompletionSource().Task.IsFaulted)
                {
                    continue;
                }

                if (context.Extensions.TryGet(out ProcessedMessage processedMessage))
                {
                    await audit.RecordProcessedMessage(processedMessage, context.Body, cancellationToken);
                }
                else if (context.Extensions.TryGet(out SagaSnapshot sagaSnapshot))
                {
                    await audit.RecordSagaSnapshot(sagaSnapshot, cancellationToken);
                }

                if (context.Extensions.TryGet<IEnumerable<EndpointDetails>>(out var newEndpoints))
                {
                    foreach (var endpointDetails in newEndpoints)
                    {
                        RecordKnownEndpoint(endpointDetails, knownEndpoints);
                    }
                }

                storedContexts.Add(context);
            }

            foreach (var endpoint in knownEndpoints.Values)
            {
                await monitoring.RecordKnownEndpoint(endpoint, cancellationToken);
            }

            return storedContexts;
        }

        async Task ProcessMessage(MessageContext context, IMessageDispatcher dispatcher, CancellationToken cancellationToken)
        {
            if (context.Headers.TryGetValue(Headers.EnclosedMessageTypes, out var messageType)
                && messageType == typeof(SagaUpdatedMessage).FullName)
            {
                ProcessSagaAuditMessage(context);
            }
            else
            {
                await ProcessAuditMessage(context, dispatcher, cancellationToken);
            }
        }

        void ProcessSagaAuditMessage(MessageContext context)
        {
            try
            {
                using var stream = new ReadOnlyStream(context.Body);
                var message = JsonSerializer.Deserialize(stream, SagaAuditMessagesSerializationContext.Default.SagaUpdatedMessage);

                var sagaSnapshot = SagaSnapshotFactory.Create(message);

                context.Extensions.Set("AuditType", "SagaSnapshot");
                context.Extensions.Set(sagaSnapshot);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Processing of saga audit message '{NativeMessageId}' failed", context.NativeMessageId);

                // releasing the failed message context early so that they can be retried outside the current batch
                context.GetTaskCompletionSource().TrySetException(e);
            }
        }

        async Task ProcessAuditMessage(MessageContext context, IMessageDispatcher dispatcher, CancellationToken cancellationToken)
        {
            if (!context.Headers.TryGetValue(Headers.MessageId, out var messageId))
            {
                messageId = DeterministicGuid.MakeId(context.NativeMessageId).ToString();
            }

            try
            {
                var metadata = new Dictionary<string, object>
                {
                    ["MessageId"] = messageId,
                    ["MessageIntent"] = context.Headers.MessageIntent()
                };

                var messagesToEmit = new List<TransportOperation>();
                var enricherContext = new AuditEnricherContext(context.Headers, messagesToEmit, metadata);

                foreach (var enricher in enrichers)
                {
                    enricher.Enrich(enricherContext);
                }

                var auditMessage = new ProcessedMessage(context.Headers, new Dictionary<string, object>(metadata));

                //Do not hook into the incoming transaction
                await dispatcher.Dispatch(new TransportOperations([.. messagesToEmit]), new TransportTransaction(), cancellationToken);

                context.Extensions.Set("AuditType", "ProcessedMessage");
                context.Extensions.Set(auditMessage);
                context.Extensions.Set(enricherContext.NewEndpoints);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Processing of message '{MessageId}' failed", messageId);

                // releasing the failed message context early so that they can be retried outside the current batch
                context.GetTaskCompletionSource().TrySetException(e);
            }
        }

        static void RecordKnownEndpoint(EndpointDetails observedEndpoint, Dictionary<string, KnownEndpoint> observedEndpoints)
        {
            var uniqueEndpointId = $"{observedEndpoint.Name}{observedEndpoint.HostId}";
            if (!observedEndpoints.ContainsKey(uniqueEndpointId))
            {
                observedEndpoints.Add(uniqueEndpointId, new KnownEndpoint
                {
                    EndpointDetails = observedEndpoint,
                    HostDisplayName = observedEndpoint.Host,
                    Monitored = false
                });
            }
        }
    }
}
