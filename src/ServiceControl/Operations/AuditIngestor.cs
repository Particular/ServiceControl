﻿namespace ServiceControl.Operations
{
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;

    public class AuditIngestor
    {
        static ILog log = LogManager.GetLogger<ErrorIngestor>();
        AuditImporter auditImporter;
        IDocumentStore store;
        IForwardMessages messageForwarder;
        Settings settings;

        public AuditIngestor(AuditImporter auditImporter, IDocumentStore store, IForwardMessages messageForwarder, Settings settings)
        {
            this.auditImporter = auditImporter;
            this.store = store;
            this.messageForwarder = messageForwarder;
            this.settings = settings;
        }

        public async Task Ingest(MessageContext context)
        {
            log.DebugFormat("Ingesting audit message {0}", context.MessageId);
            
            var processedMessage = await auditImporter.ConvertToSaveMessage(context)
                .ConfigureAwait(false);

            using (var session = store.OpenAsyncSession())
            {
                await session.StoreAsync(processedMessage)
                    .ConfigureAwait(false);
                await session.SaveChangesAsync()
                    .ConfigureAwait(false);
            }

            if (settings.ForwardAuditMessages)
            {
                // TODO: Clean the time to be received header? The old implementation went through TransportMessageCleaner
                await messageForwarder.Forward(context, settings.AuditLogQueue)
                    .ConfigureAwait(false);
            }
        }
    }
}