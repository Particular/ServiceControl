namespace ServiceControl.Operations
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Extensibility;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using Raven.Client;
    using Raven.Client.Indexes;

    public class ImportFailedAudits
    {
        public ImportFailedAudits(IDocumentStore store, AuditImporter auditImporter)
        {
            this.store = store;
            this.auditImporter = auditImporter;
        }

        public Task Run(CancellationTokenSource tokenSource)
        {
            source = tokenSource;
            return Task.Run(() => Run<FailedAuditImport, FailedAuditImportIndex>(source.Token));
        }

        async Task Run<T, I>(CancellationToken token) where I : AbstractIndexCreationTask, new()
        {
            var succeeded = 0;
            var failed = 0;
            using (var session = store.OpenAsyncSession())
            {
                var query = session.Query<T, I>();
                using (var ie = await session.Advanced.StreamAsync(query, token)
                    .ConfigureAwait(false))
                {
                    while (!token.IsCancellationRequested && await ie.MoveNextAsync().ConfigureAwait(false))
                    {
                        FailedTransportMessage dto = ((dynamic)ie.Current.Document).Message;
                        try
                        {
                            var messageContext = new MessageContext(dto.Id, dto.Headers, dto.Body, EmptyTransaction, EmptyTokenSource, EmptyContextBag);                            
                            var entity = await auditImporter.ConvertToSaveMessage(messageContext)
                                .ConfigureAwait(false);
                            using (var storeSession = store.OpenAsyncSession())
                            {
                                await storeSession.StoreAsync(entity, token).ConfigureAwait(false);
                                await storeSession.SaveChangesAsync(token).ConfigureAwait(false);
                            }
                            await store.AsyncDatabaseCommands.DeleteAsync(ie.Current.Key, null, token)
                                .ConfigureAwait(false);
                            succeeded++;
                            Logger.Info($"Successfully re-imported failed audit message {dto.Id}.");
                        }
                        catch (Exception e)
                        {
                            Logger.Error($"Error while attempting to re-import failed audit message {dto.Id}.", e);
                            failed++;
                        }
                    }
                }
            }
            Logger.Info($"Done re-importing failed audits. Successfully re-imported {succeeded} messaged. Failed re-importing {failed} messages.");

            if (failed > 0)
            {
                Logger.Warn($"{failed} messages could not be re-imported. This could indicate a problem with the data. Contact Particular support if you need help in recovering the messages.");
            }
        }

        IDocumentStore store;
        AuditImporter auditImporter;
        CancellationTokenSource source;
        
        static TransportTransaction EmptyTransaction = new TransportTransaction();
        static CancellationTokenSource EmptyTokenSource = new CancellationTokenSource();
        static ContextBag EmptyContextBag = new ContextBag();
        static readonly ILog Logger = LogManager.GetLogger(typeof(ImportFailedAudits));
    }

    class FailedAuditImportIndex : AbstractIndexCreationTask<FailedAuditImport>
    {
        public FailedAuditImportIndex()
        {
            Map = docs => from cc in docs
                select new FailedAuditImport
                {
                    Id = cc.Id,
                    Message = cc.Message
                };

            DisableInMemoryIndexing = true;
        }
    }
}