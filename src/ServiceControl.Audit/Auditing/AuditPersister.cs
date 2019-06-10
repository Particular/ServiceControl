namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using BodyStorage;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.Transport;
    using Raven.Client;

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
                ["MessageIntent"] = context.Message.Headers.MessageIntent(),
            };

            var enricherContext = new AuditEnricherContext(context.Message.Headers, context.MessageSession, metadata);
            var enricherTasks = new List<Task>(enrichers.Length);

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var enricher in enrichers)
            {
                enricherTasks.Add(enricher.Enrich(enricherContext));
            }

            await Task.WhenAll(enricherTasks)
                .ConfigureAwait(false);

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
        }

        readonly IEnrichImportedAuditMessages[] enrichers;
        readonly IDocumentStore store;
        readonly BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher;
    }
}