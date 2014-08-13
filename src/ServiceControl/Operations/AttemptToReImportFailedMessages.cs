namespace ServiceControl.Operations
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Transports;
    using Raven.Client;
    using Raven.Client.Indexes;
    using ServiceBus.Management.Infrastructure.Settings;

    class AttemptToReImportFailedMessages : IWantToRunWhenBusStartsAndStops
    {
        public AttemptToReImportFailedMessages(IDocumentStore store, ISendMessages messageSender)
        {
            this.store = store;
            this.messageSender = messageSender;
        }

        public void Start()
        {
            source = new CancellationTokenSource();

            t1 = Task.Factory.StartNew(() => Run<FailedAuditImport, FailedAuditImportIndex>(Settings.AuditQueue, source.Token), source.Token);
            t2 = Task.Factory.StartNew(() => Run<FailedErrorImport, FailedErrorImportIndex>(Settings.ErrorQueue, source.Token), source.Token);
        }

        public void Stop()
        {
            source.Cancel();
            Task.WaitAll(t1, t2);
            source.Dispose();
        }

        void Run<T, I>(Address queue, CancellationToken token) where I : AbstractIndexCreationTask, new()
        {
            using (var session = store.OpenSession())
            {
                var query = session.Query<T, I>();
                using (var ie = session.Advanced.Stream(query))
                {
                    while (!token.IsCancellationRequested && ie.MoveNext())
                    {
                        var transportMessage = ((dynamic) ie.Current.Document).Message;

                        messageSender.Send(transportMessage, queue);

                        store.DatabaseCommands.Delete(ie.Current.Key, null);
                    }
                }
            }
        }

        readonly ISendMessages messageSender;
        readonly IDocumentStore store;
        CancellationTokenSource source;
        Task t1, t2;
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