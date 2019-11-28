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

        public async Task Persist(ProcessAuditMessageContext context)
        {
            if (!context.Message.Headers.TryGetValue(Headers.MessageId, out var messageId))
            {
                messageId = DeterministicGuid.MakeId(context.Message.MessageId).ToString();
            }

            var metadata = new ConcurrentDictionary<string, object>
            {
                ["MessageId"] = messageId,
                ["MessageIntent"] = context.Message.Headers.MessageIntent()
            };

            var commandsToEmit = new List<ICommand>();
            var enricherContext = new AuditEnricherContext(context.Message.Headers, commandsToEmit, metadata);

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var enricher in enrichers)
            {
                enricher.Enrich(enricherContext);
            }

            await bodyStorageEnricher.StoreAuditMessageBody(context.Message.Body, context.Message.Headers, metadata)
                .ConfigureAwait(false);

            var auditMessage = new ProcessedMessage(context.Message.Headers, new Dictionary<string, object>(metadata))
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
                await context.MessageSession.Send(commandToEmit)
                    .ConfigureAwait(false);
            }
        }

        public async Task Persist(List<ProcessAuditMessageContext> contexts)
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
            Log.Warn($"Took {stopwatch.ElapsedMilliseconds}");
        }

        async Task<ProcessedMessage> ProcessedMessage(ProcessAuditMessageContext context, BulkInsertOperation bulkInsert)
        {
            if (!context.Message.Headers.TryGetValue(Headers.MessageId, out var messageId))
            {
                messageId = DeterministicGuid.MakeId(context.Message.MessageId).ToString();
            }

            var metadata = new ConcurrentDictionary<string, object>
            {
                ["MessageId"] = messageId,
                ["MessageIntent"] = context.Message.Headers.MessageIntent()
            };

            var commandsToEmit = new List<ICommand>();
            var enricherContext = new AuditEnricherContext(context.Message.Headers, commandsToEmit, metadata);

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var enricher in enrichers)
            {
                enricher.Enrich(enricherContext);
            }

            await bodyStorageEnricher.StoreAuditMessageBody(context.Message.Body, context.Message.Headers, metadata)
                .ConfigureAwait(false);

            var auditMessage = new ProcessedMessage(context.Message.Headers, new Dictionary<string, object>(metadata))
            {
                // We do this so Raven does not spend time assigning a hilo key
                Id = $"ProcessedMessages/{Guid.NewGuid()}"
            };

            foreach (var commandToEmit in commandsToEmit)
            {
                await context.MessageSession.Send(commandToEmit)
                    .ConfigureAwait(false);
            }

            return auditMessage;
        }

        readonly IEnrichImportedAuditMessages[] enrichers;
        readonly IDocumentStore store;
        readonly BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher;

        static ILog Log = LogManager.GetLogger<AuditPersister>();
    }
}