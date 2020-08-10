﻿namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using BodyStorage;
    using Infrastructure;
    using Monitoring;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using Raven.Client.Document;

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

            using (var bulkInsert = store.BulkInsert(options: new BulkInsertOptions
            {
                OverwriteExisting = true
            }))
            {
                var inserts = new List<Task>(contexts.Count);
                foreach (var context in contexts)
                {
                    inserts.Add(ProcessedMessage(context, bulkInsert));
                }

                await Task.WhenAll(inserts).ConfigureAwait(false);

                var knownEndpoints = new Dictionary<string, KnownEndpoint>();
                var storedContexts = new List<MessageContext>(contexts.Count);
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

                    bulkInsert.Store(processedMessage);
                    storedContexts.Add(context);
                }

                foreach (var endpoint in knownEndpoints.Values)
                {
                    bulkInsert.Store(endpoint);
                }

                try
                {
                    await bulkInsert.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    storedContexts.Clear();
                    // let's give up
                    foreach (var context in contexts)
                    {
                        context.Extensions.Get<TaskCompletionSource<bool>>().TrySetException(e);
                    }
                }

                stopwatch.Stop();
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug($"Batch size {contexts.Count} took {stopwatch.ElapsedMilliseconds}");
                }
                return storedContexts;
            }
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
                    Id = KnownEndpoint.MakeId(observedEndpoint.Name, observedEndpoint.HostId),
                };
                observedEndpoints.Add(uniqueEndpointId, knownEndpoint);
            }

            knownEndpoint.LastSeen = processedMessage.ProcessedAt > knownEndpoint.LastSeen ? processedMessage.ProcessedAt : knownEndpoint.LastSeen;
        }

        async Task ProcessedMessage(MessageContext context, BulkInsertOperation bulkInsert)
        {
            try
            {
                if (!context.Headers.TryGetValue(Headers.MessageId, out var messageId))
                {
                    messageId = DeterministicGuid.MakeId(context.MessageId).ToString();
                }

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

                await bodyStorageEnricher.StoreAuditMessageBody(context.Body, context.Headers, metadata)
                    .ConfigureAwait(false);

                var auditMessage = new ProcessedMessage(context.Headers, new Dictionary<string, object>(metadata))
                {
                    // We do this so Raven does not spend time assigning a hilo key
                    Id = $"ProcessedMessages/{Guid.NewGuid()}"
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
                context.Extensions.Get<TaskCompletionSource<bool>>().TrySetException(e);
            }
        }

        public async Task Persist(MessageContext message)
        {
            if (!message.Headers.TryGetValue(Headers.MessageId, out var messageId))
            {
                messageId = DeterministicGuid.MakeId(message.MessageId).ToString();
            }

            var metadata = new ConcurrentDictionary<string, object>
            {
                ["MessageId"] = messageId,
                ["MessageIntent"] = message.Headers.MessageIntent()
            };

            var commandsToEmit = new List<ICommand>();
            var enricherContext = new AuditEnricherContext(message.Headers, commandsToEmit, metadata);

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var enricher in enrichers)
            {
                enricher.Enrich(enricherContext);
            }

            await bodyStorageEnricher.StoreAuditMessageBody(message.Body, message.Headers, metadata)
                .ConfigureAwait(false);

            var auditMessage = new ProcessedMessage(message.Headers, new Dictionary<string, object>(metadata))
            {
                // We do this so Raven does not spend time assigning a hilo key
                Id = $"ProcessedMessages/{Guid.NewGuid()}"
            };

            using (var session = store.OpenAsyncSession())
            {
                await session.StoreAsync(auditMessage)
                    .ConfigureAwait(false);
                await session.SaveChangesAsync()
                    .ConfigureAwait(false);
            }

            foreach (var commandToEmit in commandsToEmit)
            {
                await messageSession.Send(commandToEmit)
                    .ConfigureAwait(false);
            }
        }

        readonly IEnrichImportedAuditMessages[] enrichers;
        readonly IDocumentStore store;
        readonly BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher;
        IMessageSession messageSession;
        static ILog Logger = LogManager.GetLogger<AuditPersister>();

        class EndpointDetailsComparer : IEqualityComparer<EndpointDetails>
        {
            public static EndpointDetailsComparer Instance = new EndpointDetailsComparer();

            public bool Equals(EndpointDetails x, EndpointDetails y)
            {
                return y != null && x != null && x.Name.Equals(y.Name, StringComparison.Ordinal) && x.HostId == y.HostId;
            }

            public int GetHashCode(EndpointDetails obj)
            {
                return obj.GetHashCode();
            }
        }
    }
}