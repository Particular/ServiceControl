namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure;
    using Microsoft.Extensions.Logging;
    using Monitoring;
    using NServiceBus;
    using NServiceBus.Transport;
    using Persistence.UnitOfWork;
    using ServiceControl.Audit.Persistence.Infrastructure;
    using ServiceControl.Audit.Persistence.Monitoring;
    using ServiceControl.EndpointPlugin.Messages.SagaState;
    using ServiceControl.Infrastructure;
    using ServiceControl.SagaAudit;

    class AuditPersister(IAuditIngestionUnitOfWorkFactory unitOfWorkFactory,
        IEnrichImportedAuditMessages[] enrichers,
        IMessageSession messageSession,
        Lazy<IMessageDispatcher> messageDispatcher,
        ILogger logger)
    {
        public async Task<IReadOnlyList<MessageContext>> Persist(IReadOnlyList<MessageContext> contexts, CancellationToken cancellationToken)
        {
            var storedContexts = new List<MessageContext>(contexts.Count);
            IAuditIngestionUnitOfWork unitOfWork = null;
            try
            {
                // deliberately not using the using statement because we dispose async explicitly
                unitOfWork = await unitOfWorkFactory.StartNew(contexts.Count, cancellationToken);
                var inserts = new List<Task>(contexts.Count);
                foreach (var context in contexts)
                {
                    inserts.Add(ProcessMessage(context));
                }

                await Task.WhenAll(inserts);

                var knownEndpoints = new Dictionary<string, KnownEndpoint>();

                foreach (var context in contexts)
                {
                    // Any message context that failed during processing will have a faulted task and should be skipped
                    if (context.GetTaskCompletionSource().Task.IsFaulted)
                    {
                        continue;
                    }

                    if (context.Extensions.TryGet(out ProcessedMessage processedMessage)) //Message was an audit message
                    {
                        if (context.Extensions.TryGet("SendingEndpoint", out EndpointDetails sendingEndpoint))
                        {
                            RecordKnownEndpoints(sendingEndpoint, knownEndpoints, processedMessage);
                        }

                        if (context.Extensions.TryGet("ReceivingEndpoint", out EndpointDetails receivingEndpoint))
                        {
                            RecordKnownEndpoints(receivingEndpoint, knownEndpoints, processedMessage);
                        }

                        await unitOfWork.RecordProcessedMessage(processedMessage, context.Body, cancellationToken);
                    }
                    else if (context.Extensions.TryGet(out SagaSnapshot sagaSnapshot))
                    {
                        await unitOfWork.RecordSagaSnapshot(sagaSnapshot, cancellationToken);
                    }

                    storedContexts.Add(context);
                }

                foreach (var endpoint in knownEndpoints.Values)
                {
                    logger.LogDebug("Adding known endpoint '{EndpointName}' for bulk storage", endpoint.Name);

                    await unitOfWork.RecordKnownEndpoint(endpoint, cancellationToken);
                }
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
                    logger.LogDebug("Performing bulk session dispose");

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

            return storedContexts;
        }

        static void RecordKnownEndpoints(EndpointDetails observedEndpoint, Dictionary<string, KnownEndpoint> observedEndpoints, ProcessedMessage processedMessage)
        {
            var uniqueEndpointId = $"{observedEndpoint.Name}{observedEndpoint.HostId}";
            if (!observedEndpoints.TryGetValue(uniqueEndpointId, out var knownEndpoint))
            {
                knownEndpoint = new KnownEndpoint
                {
                    Host = observedEndpoint.Host,
                    HostId = observedEndpoint.HostId,
                    LastSeen = processedMessage.ProcessedAt,
                    Name = observedEndpoint.Name,
                    Id = KnownEndpoint.MakeDocumentId(observedEndpoint.Name, observedEndpoint.HostId),
                };
                observedEndpoints.Add(uniqueEndpointId, knownEndpoint);
            }

            knownEndpoint.LastSeen = processedMessage.ProcessedAt > knownEndpoint.LastSeen ? processedMessage.ProcessedAt : knownEndpoint.LastSeen;
        }

        async Task ProcessMessage(MessageContext context)
        {
            if (context.Headers.TryGetValue(Headers.EnclosedMessageTypes, out var messageType)
                && messageType == typeof(SagaUpdatedMessage).FullName)
            {
                ProcessSagaAuditMessage(context);
            }
            else
            {
                await ProcessAuditMessage(context);
            }
        }

        void ProcessSagaAuditMessage(MessageContext context)
        {
            try
            {
                using var stream = new ReadOnlyStream(context.Body);
                SagaUpdatedMessage message = JsonSerializer.Deserialize(stream, SagaAuditMessagesSerializationContext.Default.SagaUpdatedMessage);

                var sagaSnapshot = SagaSnapshotFactory.Create(message);

                context.Extensions.Set("AuditType", "SagaSnapshot");
                context.Extensions.Set(sagaSnapshot);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Processing of saga audit message '{NativeMessageId}' failed.", context.NativeMessageId);

                // releasing the failed message context early so that they can be retried outside the current batch
                context.GetTaskCompletionSource().TrySetException(e);
            }
        }

        async Task ProcessAuditMessage(MessageContext context)
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

                var commandsToEmit = new List<ICommand>();
                var messagesToEmit = new List<TransportOperation>();
                var enricherContext = new AuditEnricherContext(context.Headers, commandsToEmit, messagesToEmit, metadata);

                foreach (var enricher in enrichers)
                {
                    enricher.Enrich(enricherContext);
                }

                var auditMessage = new ProcessedMessage(context.Headers, new Dictionary<string, object>(metadata));

                logger.LogDebug("Emitting {CommandsToEmitCount} commands and {MessagesToEmitCount} control messages.", commandsToEmit.Count, messagesToEmit.Count);

                foreach (var commandToEmit in commandsToEmit)
                {
                    await messageSession.Send(commandToEmit);
                }

                await messageDispatcher.Value.Dispatch(new TransportOperations(messagesToEmit.ToArray()),
                    new TransportTransaction()); //Do not hook into the incoming transaction

                logger.LogDebug("{CommandsToEmitCount} commands and {MessagesToEmitCount} control messages emitted.", commandsToEmit.Count, messagesToEmit.Count);

                if (metadata.TryGetValue("SendingEndpoint", out var sendingEndpoint))
                {
                    context.Extensions.Set("SendingEndpoint", (EndpointDetails)sendingEndpoint);
                }

                if (metadata.TryGetValue("ReceivingEndpoint", out var receivingEndpoint))
                {
                    context.Extensions.Set("ReceivingEndpoint", (EndpointDetails)receivingEndpoint);
                }

                context.Extensions.Set("AuditType", "ProcessedMessage");
                context.Extensions.Set(auditMessage);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Processing of message '{MessageId}' failed.", messageId);

                // releasing the failed message context early so that they can be retried outside the current batch
                context.GetTaskCompletionSource().TrySetException(e);
            }
        }
    }
}