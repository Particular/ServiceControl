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

    class AttemptToReImportFailedMessages: IWantToRunWhenBusStartsAndStops
    {
        readonly IDocumentStore store;
        readonly ISendMessages messageSender;
        CancellationTokenSource source;

        public AttemptToReImportFailedMessages(IDocumentStore store, ISendMessages messageSender)
        {
            this.store = store;
            this.messageSender = messageSender;
        }

        public void Start()
        {
            source = new CancellationTokenSource();

            Task.Factory.StartNew(() => Run<FailedAuditImport, FailedAuditImportIndex>(Settings.AuditQueue), source.Token);
            Task.Factory.StartNew(() => Run<FailedErrorImport, FailedErrorImportIndex>(Settings.ErrorQueue), source.Token);
        }

        public void Stop()
        {
            source.Cancel();
        }

        void Run<T, I>(Address queue) where I : AbstractIndexCreationTask, new()
        {
            var session = store.OpenSession();
            var query = session.Query<T, I>();
            using (var ie = session.Advanced.Stream(query))
            {
                while (ie.MoveNext())
                {
                    var transportMessage = ((dynamic)ie.Current.Document).Message;

                    messageSender.Send(transportMessage, queue);

                    store.DatabaseCommands.Delete(ie.Current.Key, null);
                }
            }
        }
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
