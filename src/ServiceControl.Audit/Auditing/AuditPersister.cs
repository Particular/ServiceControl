namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using BodyStorage;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using Raven.Client.Document;
    using Transports;

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

        public async Task Persist(IReadOnlyCollection<BatchMessage> contexts)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            Log.Warn($"Batch size {contexts.Count}");

            using (var bulkInsert = store.BulkInsert(options: new BulkInsertOptions { BatchSize = contexts.Count }))
            {
                var inserts = new List<Task<ProcessedMessage>>();
                foreach (var context in contexts)
                {
                    inserts.Add(ProcessedMessage(context, bulkInsert));
                }
                await Task.WhenAll(inserts).ConfigureAwait(false);

                foreach (var message in inserts)
                {
                    var processedMessage = await message.ConfigureAwait(false);
                    bulkInsert.Store(processedMessage);
                }

                await bulkInsert.DisposeAsync().ConfigureAwait(false);
            }
            stopwatch.Stop();
            Log.Warn($"Took {stopwatch.ElapsedMilliseconds} ms");
        }

        async Task<ProcessedMessage> ProcessedMessage(BatchMessage batchMessage, BulkInsertOperation bulkInsert)
        {
            if (!batchMessage.Headers.TryGetValue(Headers.MessageId, out var messageId))
            {
                messageId = DeterministicGuid.MakeId(batchMessage.MessageId).ToString();
            }

            var metadata = new ConcurrentDictionary<string, object>
            {
                ["MessageId"] = messageId,
                ["MessageIntent"] = batchMessage.Headers.MessageIntent()
            };

            var commandsToEmit = new List<ICommand>();
            var enricherContext = new AuditEnricherContext(batchMessage.Headers, commandsToEmit, metadata);

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var enricher in enrichers)
            {
                enricher.Enrich(enricherContext);
            }

            await bodyStorageEnricher.StoreAuditMessageBody(batchMessage.Body, batchMessage.Headers, metadata)
                .ConfigureAwait(false);

            var auditMessage = new ProcessedMessage(batchMessage.Headers, new Dictionary<string, object>(metadata))
            {
                // We do this so Raven does not spend time assigning a hilo key
                Id = $"ProcessedMessages/{Guid.NewGuid()}"
            };

            foreach (var commandToEmit in commandsToEmit)
            {
                await messageSession.Send(commandToEmit)
                    .ConfigureAwait(false);
            }

            return auditMessage;
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
        static ILog Log = LogManager.GetLogger<AuditPersister>();
    }
}