namespace ServiceControl.Operations
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Logging;
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
            return Task.Factory.StartNew(() => Run<FailedAuditImport, FailedAuditImportIndex>(source.Token));
        }

        void Run<T, I>(CancellationToken token) where I : AbstractIndexCreationTask, new()
        {
            var succeeded = 0;
            var failed = 0;
            using (var session = store.OpenSession())
            {
                var query = session.Query<T, I>();
                using (var ie = session.Advanced.Stream(query))
                {
                    while (!token.IsCancellationRequested && ie.MoveNext())
                    {
                        FailedTransportMessage dto = ((dynamic)ie.Current.Document).Message;
                        try
                        {
                            var transportMessage = new TransportMessage(dto.Id, dto.Headers)
                            {
                                Body = dto.Body
                            };

                            var entity = auditImporter.ConvertToSaveMessage(transportMessage);
                            using (var storeSession = store.OpenSession())
                            {
                                storeSession.Store(entity);
                                storeSession.SaveChanges();
                            }
                            store.DatabaseCommands.Delete(ie.Current.Key, null);
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
        }

        IDocumentStore store;
        AuditImporter auditImporter;
        CancellationTokenSource source;
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