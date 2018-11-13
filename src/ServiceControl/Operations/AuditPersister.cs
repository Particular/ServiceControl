namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using BodyStorage;
    using Infrastructure;
    using MessageAuditing;
    using NServiceBus;
    using NServiceBus.Transport;
    using Raven.Client;


    class AuditPersister
    {
        public AuditPersister(IDocumentStore store, BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher, IEnrichImportedMessages[] enrichers)
        {
            this.store = store;
            this.bodyStorageEnricher = bodyStorageEnricher;
            this.enrichers = enrichers.Where(e => e.EnrichAudits).ToArray();
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
                ["MessageIntent"] = message.Headers.MessageIntent(),
            };

            var enricherTasks = new List<Task>(enrichers.Length);
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var enricher in enrichers)
            {
                enricherTasks.Add(enricher.Enrich(message.Headers, metadata));
            }

            await Task.WhenAll(enricherTasks)
                .ConfigureAwait(false);

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
        }

        readonly IEnrichImportedMessages[] enrichers;
        readonly IDocumentStore store;
        readonly BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher;
    }
}