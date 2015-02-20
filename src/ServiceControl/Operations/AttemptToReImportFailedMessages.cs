namespace ServiceControl.Operations
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Client;
    using Raven.Client.Indexes;

    class AttemptToReImportFailedMessages : IWantToRunWhenBusStartsAndStops
    {
        public AttemptToReImportFailedMessages(IDocumentStore store)
        {
            this.store = store;
        }

        public void Start()
        {
            source = new CancellationTokenSource();
            //We skip this logging task for audits as there is no potential message loss there
            t2 = Task.Factory.StartNew(() => Run<FailedErrorImport, FailedErrorImportIndex>(source.Token), source.Token);
        }

        public void Stop()
        {
            source.Cancel();
            Task.WaitAll(t2);
            source.Dispose();
        }

        void Run<T, I>(CancellationToken token) where I : AbstractIndexCreationTask, new()
        {
            using (var session = store.OpenSession())
            {
                var query = session.Query<T, I>();
                using (var ie = session.Advanced.Stream(query))
                {
                    if (!token.IsCancellationRequested && ie.MoveNext())
                    {
                        Logger.Warn(@"One or more error messages have previously failed to import properly into ServiceControl and have been stored in ServiceControl database.
Due to a defect however, ServiceControl would not be able to automatically reimport them. Please run ServiceControl in the maintenance mode and use embedded RavenStudio available by default at http://localhost:33333/storage to examine the payloads of failed messages to ensure no information has been lost.
Delete the failed import documents afterwards so that you don't see this warning message again.");
                    }
                }
            }
        }

        readonly IDocumentStore store;
        CancellationTokenSource source;
        Task t2;
        static readonly ILog Logger = LogManager.GetLogger(typeof(AttemptToReImportFailedMessages));
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

    class FailedErrorImportIndex : AbstractIndexCreationTask<FailedErrorImport>
    {
        public FailedErrorImportIndex()
        {
            Map = docs => from cc in docs
                select new FailedErrorImport
                {
                    Id = cc.Id,
                    Message = cc.Message
                };

            DisableInMemoryIndexing = true;
        }
    }
}
