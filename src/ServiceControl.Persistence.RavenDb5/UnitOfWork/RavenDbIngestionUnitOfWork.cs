namespace ServiceControl.Persistence.RavenDb
{
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using ServiceControl.Persistence.UnitOfWork;
    using Raven.Client.Documents.Commands.Batches;
    using RavenDb5;

    class RavenDbIngestionUnitOfWork : IngestionUnitOfWorkBase
    {
        readonly DocumentStoreProvider storeProvider;
        readonly ConcurrentBag<ICommandData> commands;

        public RavenDbIngestionUnitOfWork(DocumentStoreProvider storeProvider)
        {
            this.storeProvider = storeProvider;
            commands = new ConcurrentBag<ICommandData>();
            Monitoring = new RavenDbMonitoringIngestionUnitOfWork(this);
            Recoverability = new RavenDbRecoverabilityIngestionUnitOfWork(this);
        }

        internal void AddCommand(ICommandData command) => commands.Add(command);

        public override async Task Complete()
        {
            using (var session = storeProvider.Store.OpenAsyncSession())
            {
                // not really interested in the batch results since a batch is atomic
                session.Advanced.Defer(commands.ToArray());
                await session.SaveChangesAsync();
            }
        }
    }
}