namespace ServiceControl.Operations
{
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Transports;
    using Raven.Client;
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

            Task.Factory.StartNew(() => Run<FailedAuditImport>(Settings.AuditQueue), source.Token);
            Task.Factory.StartNew(() => Run<FailedErrorImport>(Settings.ErrorQueue), source.Token);
        }

        public void Stop()
        {
            source.Cancel();
        }

        void Run<T>(Address queue)
        {
            var session = store.OpenSession();
            var query = session.Query<T>();
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
}
