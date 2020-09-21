using Raven.Client.Documents.BulkInsert;
using ServiceControl.Infrastructure;

namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
    using BodyStorage;
    using EndpointPlugin.Messages.SagaState;
    using Infrastructure;
    using Microsoft.IO;
    using Monitoring;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using Raven.Client;
    using Raven.Client.Documents;
    using Raven.Client.Json;
    using ServiceControl.SagaAudit;
    using JsonSerializer = Newtonsoft.Json.JsonSerializer;

    class AuditPersister
    {
        public AuditPersister(IDocumentStore store, BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher, IEnrichImportedAuditMessages[] enrichers, TimeSpan auditRetentionPeriod,
            Counter ingestedAuditMeter, Counter ingestedSagaAuditMeter, Meter auditBulkInsertDurationMeter, Meter sagaAuditBulkInsertDurationMeter, Meter bulkInsertCommitDurationMeter)
        {
            this.store = store;
            this.bodyStorageEnricher = bodyStorageEnricher;
            this.enrichers = enrichers;
            this.auditRetentionPeriod = auditRetentionPeriod;
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

        public async Task<IReadOnlyList<MessageContext>> Persist(List<MessageContext> contexts)
        {
            var stopwatch = Stopwatch.StartNew();

            if (Logger.IsDebugEnabled)
            {
                Logger.Debug($"Batch size {contexts.Count}");
            }

            var storedContexts = new List<MessageContext>(contexts.Count);
            BulkInsertOperation bulkInsert = null;
            try
            {
                bulkInsert = store.BulkInsert();
                var inserts = new List<Task>(contexts.Count);
                foreach (var context in contexts)
                {
                    inserts.Add(ProcessMessage(context));
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

                        using (auditBulkInsertDurationMeter.Measure())
                        {
                            if (Logger.IsDebugEnabled)
                            {
                                Logger.Debug($"Adding audit message for bulk storage");
                            }
                            await bulkInsert.StoreAsync(processedMessage, GetExpirationMetadata())
                                .ConfigureAwait(false);

                            using (var stream = Memory.Manager.GetStream(Guid.NewGuid(), processedMessage.Id, context.Body, 0, context.Body.Length))
                            {
                                if (processedMessage.MessageMetadata.ContentType != null)
                                {
                                    await bulkInsert.AttachmentsFor(processedMessage.Id)
                                        .StoreAsync("body", stream, (string)processedMessage.MessageMetadata.ContentType).ConfigureAwait(false);
                                }
                                else
                                {
                                    await bulkInsert.AttachmentsFor(processedMessage.Id).StoreAsync("body", stream)
                                        .ConfigureAwait(false);
                                }
                            }
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
                            await bulkInsert.StoreAsync(sagaSnapshot, GetExpirationMetadata()).ConfigureAwait(false);
                        }
                        storedContexts.Add(context);
                        ingestedSagaAuditMeter.Mark();
                    }
                }

                await StoreKnownEndpoints(knownEndpoints, bulkInsert).ConfigureAwait(false);

                using (bulkInsertCommitDurationMeter.Measure())
                {
                    await bulkInsert.DisposeAsync().ConfigureAwait(false);
                    bulkInsert = null;
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
                        await bulkInsert.DisposeAsync().ConfigureAwait(false);
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
                }

                stopwatch.Stop();
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Batch size {contexts.Count} took {stopwatch.ElapsedMilliseconds}");
                }
            }

            return storedContexts;
        }

        async Task StoreKnownEndpoints(Dictionary<string, KnownEndpoint> knownEndpoints, BulkInsertOperation bulkInsert)
        {
            foreach (var endpoint in knownEndpoints)
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Adding known endpoint for bulk storage");
                }
                if (endpointInfoPersistTimes.TryGetValue(endpoint.Key, out var timePersisted) 
                    && timePersisted.AddSeconds(30) > DateTime.UtcNow)
                {
                    //Only store endpoint if it has not been stored in last 30 seconds
                    continue;
                }

                await bulkInsert.StoreAsync(
                    endpoint.Value,
                    new MetadataAsDictionary(new Dictionary<string, object>
                    {
                        [Constants.Documents.Metadata.Expires] = endpoint.Value.LastSeen.Add(auditRetentionPeriod).ToString("O")
                    })
                ).ConfigureAwait(false);

                endpointInfoPersistTimes[endpoint.Key] = DateTime.UtcNow;
            }
        }

        MetadataAsDictionary GetExpirationMetadata()
        {
            return new MetadataAsDictionary
            {
                [Constants.Documents.Metadata.Expires] = DateTime.UtcNow.Add(auditRetentionPeriod)
            };
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
                await ProcessAuditMessage(context).ConfigureAwait(false);
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

        async Task ProcessAuditMessage(MessageContext context)
        {
            if (!context.Headers.TryGetValue(Headers.MessageId, out var messageId))
            {
                messageId = DeterministicGuid.MakeId(context.MessageId).ToString();
            }

            try
            {
                var messageData = new ProcessedMessageData
                {
                    MessageId = messageId,
                    MessageIntent = context.Headers.MessageIntent()
                };

                var commandsToEmit = new List<ICommand>();
                var enricherContext = new AuditEnricherContext(context.Headers, commandsToEmit, messageData);

                foreach (var enricher in enrichers)
                {
                    enricher.Enrich(enricherContext);
                }

                var processingStartedTicks =
                    context.Headers.TryGetValue(Headers.ProcessingStarted, out var processingStartedValue)
                        ? DateTimeExtensions.ToUtcDateTime(processingStartedValue).Ticks
                        : DateTime.UtcNow.Ticks;

                var documentId = $"{processingStartedTicks}-{context.Headers.ProcessingId()}";

                bodyStorageEnricher.StoreAuditMessageBody(documentId, context.Body, context.Headers, messageData);

                var auditMessage = new ProcessedMessage(context.Headers, messageData)
                {
                    Id = $"ProcessedMessages/{documentId}"
                };

                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Emitting {commandsToEmit.Count} commands");
                }
                foreach (var commandToEmit in commandsToEmit)
                {
                    await messageSession.Send(commandToEmit)
                        .ConfigureAwait(false);
                }
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"{commandsToEmit.Count} commands emitted.");
                }

                if (messageData.SendingEndpoint != null)
                {
                    context.Extensions.Set("SendingEndpoint", messageData.SendingEndpoint);
                }

                if (messageData.ReceivingEndpoint != null)
                {
                    context.Extensions.Set("ReceivingEndpoint", messageData.ReceivingEndpoint);
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

        readonly JsonSerializer sagaAuditSerializer = new JsonSerializer();
        readonly IEnrichImportedAuditMessages[] enrichers;
        readonly TimeSpan auditRetentionPeriod;
        readonly Counter ingestedAuditMeter;
        readonly Counter ingestedSagaAuditMeter;
        readonly Meter auditBulkInsertDurationMeter;
        readonly Meter sagaAuditBulkInsertDurationMeter;
        readonly Meter bulkInsertCommitDurationMeter;
        readonly IDocumentStore store;
        readonly BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher;
        readonly Dictionary<string, DateTime> endpointInfoPersistTimes = new Dictionary<string, DateTime>();
        IMessageSession messageSession;
        static ILog Logger = LogManager.GetLogger<AuditPersister>();
    }
}
