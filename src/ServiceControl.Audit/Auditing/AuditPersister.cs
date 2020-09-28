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
    using Lucene.Net.Util;
    using Monitoring;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using ServiceControl.SagaAudit;
    using JsonSerializer = Newtonsoft.Json.JsonSerializer;
    using Raven.Client.Documents;
    using Raven.Client.Json;

    class AuditPersister
    {
        public AuditPersister(IDocumentStore store, BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher, IEnrichImportedAuditMessages[] enrichers, TimeSpan auditRetentionPeriod)
        {
            this.store = store;
            this.bodyStorageEnricher = bodyStorageEnricher;
            this.enrichers = enrichers;
            this.auditRetentionPeriod = auditRetentionPeriod;
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
            try
            {
                var bulkInsert = store.BulkInsert();
                var inserts = new List<Task>(contexts.Count);
                foreach (var context in contexts)
                {
                    inserts.Add(ProcessMessage(context));
                }

                await Task.WhenAll(inserts).ConfigureAwait(false);

                var knownEndpoints = new Dictionary<string, KnownEndpoint>();

                foreach (var context in contexts)
                {
                    if (context.Extensions.TryGet("SendingEndpoint", out EndpointDetails sendingEndpoint))
                    {
                        RecordKnownEndpoints(sendingEndpoint, knownEndpoints, processedMessage);
                    }

                    if (context.Extensions.TryGet("ReceivingEndpoint", out EndpointDetails receivingEndpoint))
                    {
                        RecordKnownEndpoints(receivingEndpoint, knownEndpoints, processedMessage);
                    }

                    if (context.Extensions.TryGet(out ProcessedMessage processedMessage)) //Message was an audit message
                    {
                        await bulkInsert.StoreAsync(processedMessage, new MetadataAsDictionary
                            {
                                [Raven.Client.Constants.Documents.Metadata.Expires] = DateTime.UtcNow.Add(auditRetentionPeriod)
                            })
                            .ConfigureAwait(false);

                        using (var stream = new MemoryStream(context.Body))
                        {
                            if (processedMessage.MessageMetadata.TryGetValue("ContentType", out var contentType))
                            {
                                await bulkInsert.AttachmentsFor(processedMessage.Id).StoreAsync("body", stream, (string)contentType).ConfigureAwait(false);
                            }
                            else
                            {
                                await bulkInsert.AttachmentsFor(processedMessage.Id).StoreAsync("body", stream).ConfigureAwait(false);
                            }
                        }

                        storedContexts.Add(context);
                    }
                    else if (context.Extensions.TryGet(out SagaSnapshot sagaSnapshot))
                    {
                        await bulkInsert.StoreAsync(sagaSnapshot).ConfigureAwait(false);
                        storedContexts.Add(context);
                    }
                }

                foreach (var endpoint in knownEndpoints.Values)
                {
                    await bulkInsert.StoreAsync(endpoint).ConfigureAwait(false);
                }

                await bulkInsert.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                storedContexts.Clear();
                // let's give up
                foreach (var context in contexts)
                {
                    context.GetTaskCompletionSource().TrySetException(e);
                }

                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug("Bulk insertion failed", e);
                }
            }
            finally
            {
                stopwatch.Stop();
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Batch size {contexts.Count} took {stopwatch.ElapsedMilliseconds}");
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
                await ProcessAuditMessage(context).ConfigureAwait(false);
            }
        }

        void ProcessSagaAuditMessage(MessageContext context)
        {
            try
            {
                SagaUpdatedMessage message;
                using (var reader = new JsonTextReader(new StreamReader(new MemoryStream(context.Body))))
                {
                    message = sagaAuditSerializer.Deserialize<SagaUpdatedMessage>(reader);
                }

                var sagaSnapshot = SagaSnapshotFactory.Create(message);

                context.Extensions.Set(sagaSnapshot);
                context.Extensions.Set("AuditType", "SagaSnapshot");
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
                var metadata = new ConcurrentDictionary<string, object>
                {
                    ["MessageId"] = messageId,
                    ["MessageIntent"] = context.Headers.MessageIntent()
                };

                var commandsToEmit = new List<ICommand>();
                var enricherContext = new AuditEnricherContext(context.Headers, commandsToEmit, metadata);

                // ReSharper disable once LoopCanBeConvertedToQuery
                foreach (var enricher in enrichers)
                {
                    enricher.Enrich(enricherContext);
                }

                bodyStorageEnricher.StoreAuditMessageBody(context.Body, context.Headers, metadata);

                var auditMessage = new ProcessedMessage(context.Headers, new Dictionary<string, object>(metadata))
                {
                    Id = $"ProcessedMessages/{context.Headers.ProcessingId()}"
                };

                foreach (var commandToEmit in commandsToEmit)
                {
                    await messageSession.Send(commandToEmit)
                        .ConfigureAwait(false);
                }

                context.Extensions.Set(auditMessage);
                if (metadata.TryGetValue("SendingEndpoint", out var sendingEndpoint))
                {
                    context.Extensions.Set("SendingEndpoint", (EndpointDetails)sendingEndpoint);
                }

                if (metadata.TryGetValue("ReceivingEndpoint", out var receivingEndpoint))
                {
                    context.Extensions.Set("ReceivingEndpoint", (EndpointDetails)receivingEndpoint);
                }

                context.Extensions.Set("AuditType", "ProcessedMessage");
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
        readonly IDocumentStore store;
        readonly BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher;
        IMessageSession messageSession;
        static ILog Logger = LogManager.GetLogger<AuditPersister>();
    }
}