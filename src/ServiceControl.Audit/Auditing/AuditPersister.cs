namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
    using BodyStorage;
    using EndpointPlugin.Messages.SagaState;
    using Infrastructure;
    using Infrastructure.SQL;
    using Monitoring;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using ServiceControl.Infrastructure.Metrics;
    using ServiceControl.SagaAudit;
    using JsonSerializer = Newtonsoft.Json.JsonSerializer;

    class AuditPersister
    {
        public AuditPersister(SqlStore store, BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher, IEnrichImportedAuditMessages[] enrichers,
            Counter ingestedAuditMeter, Counter ingestedSagaAuditMeter, Meter auditBulkInsertDurationMeter, Meter sagaAuditBulkInsertDurationMeter, Meter bulkInsertCommitDurationMeter)
        {
            this.store = store;
            this.bodyStorageEnricher = bodyStorageEnricher;
            this.enrichers = enrichers;

            this.ingestedAuditMeter = ingestedAuditMeter;
            this.ingestedSagaAuditMeter = ingestedSagaAuditMeter;
            this.auditBulkInsertDurationMeter = auditBulkInsertDurationMeter;
            this.sagaAuditBulkInsertDurationMeter = sagaAuditBulkInsertDurationMeter;
            this.bulkInsertCommitDurationMeter = bulkInsertCommitDurationMeter;
        }

        public void Initialize(IMessageSession messageSession)
        {
            this.messageSession = messageSession;
        }

        public async Task<IReadOnlyList<MessageContext>> Persist(List<MessageContext> contexts, IDispatchMessages dispatcher)
        {
            var stopwatch = Stopwatch.StartNew();

            if (Logger.IsDebugEnabled)
            {
                Logger.Debug($"Batch size {contexts.Count}");
            }

            var storedContexts = new List<MessageContext>(contexts.Count);
            SqlBulkInsertOperation bulkInsert = null;
            try
            {
                bulkInsert = store.CreateBulkInsertOperation(
                    overrideExistingRows: true,
                    chunked: false,
                    chunkSize: contexts.Count);

                var inserts = new List<Task>(contexts.Count);
                foreach (var context in contexts)
                {
                    inserts.Add(ProcessMessage(context, dispatcher));
                }

                await Task.WhenAll(inserts).ConfigureAwait(false);

                var knownEndpoints = new Dictionary<string, KnownEndpoint>();

                foreach (var context in contexts)
                {
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

                        if (Logger.IsDebugEnabled)
                        {
                            Logger.Debug("Adding audit message for bulk storage");
                        }

                        using (auditBulkInsertDurationMeter.Measure())
                        {
                            await bulkInsert.StoreAsync(processedMessage).ConfigureAwait(false);
                        }

                        storedContexts.Add(context);
                        ingestedAuditMeter.Mark();
                    }
                    else if (context.Extensions.TryGet(out SagaSnapshot sagaSnapshot))
                    {
                        if (Logger.IsDebugEnabled)
                        {
                            Logger.Debug("Adding SagaSnapshot message for bulk storage");
                        }

                        using (sagaAuditBulkInsertDurationMeter.Measure())
                        {
                            await bulkInsert.StoreAsync(sagaSnapshot).ConfigureAwait(false);
                        }

                        storedContexts.Add(context);
                        ingestedSagaAuditMeter.Mark();
                    }
                }

                foreach (var endpoint in knownEndpoints.Values)
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.Debug($"Adding known endpoint '{endpoint.Name}' for bulk storage");
                    }
                    await bulkInsert.StoreAsync(endpoint).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug("Bulk insertion failed", e);
                }

                // making sure to rethrow so that all messages get marked as failed
                throw;
            }
            finally
            {
                if (bulkInsert != null)
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.Debug("Performing bulk session dispose");
                    }

                    try
                    {
                        // this can throw even though dispose is never supposed to throw
                        using (bulkInsertCommitDurationMeter.Measure())
                        {
                            await bulkInsert.DisposeAsync().ConfigureAwait(false);
                        }
                    }
                    catch (Exception e)
                    {
                        if (Logger.IsDebugEnabled)
                        {
                            Logger.Debug("Bulk insertion dispose failed", e);
                        }

                        // making sure to rethrow so that all messages get marked as failed
                        throw;
                    }
                    finally
                    {
                        stopwatch.Stop();
                    }
                }

                stopwatch.Stop();
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Batch size {contexts.Count} took {stopwatch.ElapsedMilliseconds} ms");
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

        async Task ProcessMessage(MessageContext context, IDispatchMessages dispatcher)
        {
            if (context.Headers.TryGetValue(Headers.EnclosedMessageTypes, out var messageType)
                && messageType == typeof(SagaUpdatedMessage).FullName)
            {
                ProcessSagaAuditMessage(context);
            }
            else
            {
                await ProcessAuditMessage(context, dispatcher).ConfigureAwait(false);
            }
        }

        void ProcessSagaAuditMessage(MessageContext context)
        {
            try
            {
                SagaUpdatedMessage message;
                using (var memoryStream = Memory.Manager.GetStream(context.MessageId, context.Body, 0, context.Body.Length))
                using (var streamReader = new StreamReader(memoryStream))
                using (var reader = new JsonTextReader(streamReader))
                {
                    message = sagaAuditSerializer.Deserialize<SagaUpdatedMessage>(reader);
                }

                var sagaSnapshot = SagaSnapshotFactory.Create(message);

                context.Extensions.Set("AuditType", "SagaSnapshot");
                context.Extensions.Set(sagaSnapshot);
            }
            catch (Exception e)
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Processing of saga audit message '{context.MessageId}' failed.", e);
                }

                context.GetTaskCompletionSource().TrySetException(e);
            }
        }

        async Task ProcessAuditMessage(MessageContext context, IDispatchMessages dispatcher)
        {
            if (!context.Headers.TryGetValue(Headers.MessageId, out var messageId))
            {
                messageId = DeterministicGuid.MakeId(context.MessageId).ToString();
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

                var processingStartedTicks =
                    context.Headers.TryGetValue(Headers.ProcessingStarted, out var processingStartedValue)
                        ? DateTimeExtensions.ToUtcDateTime(processingStartedValue).Ticks.ToString()
                        : DateTime.UtcNow.Ticks.ToString();

                var documentId = $"{processingStartedTicks}-{context.Headers.ProcessingId()}";

                var auditMessage = new ProcessedMessage(context.Headers, new Dictionary<string, object>(metadata))
                {
                    Id = $"ProcessedMessages/{documentId}"
                };

                await bodyStorageEnricher.StoreAuditMessageBody(context.Body, auditMessage)
                    .ConfigureAwait(false);

                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Emitting {commandsToEmit.Count} commands and {messagesToEmit.Count} control messages.");
                }
                foreach (var commandToEmit in commandsToEmit)
                {
                    await messageSession.Send(commandToEmit)
                        .ConfigureAwait(false);
                }

                await dispatcher.Dispatch(new TransportOperations(messagesToEmit.ToArray()),
                    new TransportTransaction(), //Do not hook into the incoming transaction
                    new ContextBag()).ConfigureAwait(false);

                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"{commandsToEmit.Count} commands and {messagesToEmit.Count} control messages emitted.");
                }

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
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Processing of message '{messageId}' failed.", e);
                }

                context.GetTaskCompletionSource().TrySetException(e);
            }
        }

        readonly Counter ingestedAuditMeter;
        readonly Counter ingestedSagaAuditMeter;
        readonly Meter auditBulkInsertDurationMeter;
        readonly Meter sagaAuditBulkInsertDurationMeter;
        readonly Meter bulkInsertCommitDurationMeter;

        readonly JsonSerializer sagaAuditSerializer = new JsonSerializer();
        readonly IEnrichImportedAuditMessages[] enrichers;
        readonly SqlStore store;
        readonly BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher;
        IMessageSession messageSession;
        static readonly ILog Logger = LogManager.GetLogger<AuditPersister>();
    }
}
