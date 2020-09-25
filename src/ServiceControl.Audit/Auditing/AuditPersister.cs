namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
    using BodyStorage;
    using Infrastructure;
    using Monitoring;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using Raven.Client.Documents;

    class AuditPersister
    {
        public AuditPersister(IDocumentStore store, BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher, IEnrichImportedAuditMessages[] enrichers)
        {
            this.store = store;
            this.bodyStorageEnricher = bodyStorageEnricher;
            this.enrichers = enrichers;
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
                    inserts.Add(ProcessedMessage(context));
                }

                await Task.WhenAll(inserts).ConfigureAwait(false);

                var knownEndpoints = new Dictionary<string, KnownEndpoint>();

                foreach (var context in contexts)
                {
                    if (!context.Extensions.TryGet(out ProcessedMessage processedMessage))
                    {
                        continue;
                    }

                    if (context.Extensions.TryGet("SendingEndpoint", out EndpointDetails sendingEndpoint))
                    {
                        RecordKnownEndpoints(sendingEndpoint, knownEndpoints, processedMessage);
                    }

                    if (context.Extensions.TryGet("ReceivingEndpoint", out EndpointDetails receivingEndpoint))
                    {
                        RecordKnownEndpoints(receivingEndpoint, knownEndpoints, processedMessage);
                    }

                    await bulkInsert.StoreAsync(processedMessage)
                        .ConfigureAwait(false);

                    using (var stream = new MemoryStream(context.Body))
                    {
                        await bulkInsert.AttachmentsFor(processedMessage.Id).StoreAsync(
                            "body", 
                            stream, 
                            (string)processedMessage.MessageMetadata["ContentType"]
                        ).ConfigureAwait(false);
                    }

                    
                    storedContexts.Add(context);
                }

                foreach (var endpoint in knownEndpoints.Values)
                {
                    await bulkInsert.StoreAsync(endpoint)
                        .ConfigureAwait(false);
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

        async Task ProcessedMessage(MessageContext context)
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
                    // We do this so Raven does not spend time assigning a hilo key
                    Id = $"ProcessedMessages/{context.Headers.UniqueId()}"
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

        readonly IEnrichImportedAuditMessages[] enrichers;
        readonly IDocumentStore store;
        readonly BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher;
        IMessageSession messageSession;
        static ILog Logger = LogManager.GetLogger<AuditPersister>();
    }
}