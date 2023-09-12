namespace ServiceControl.Persistence.RavenDb
{
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using ServiceControl.Persistence.UnitOfWork;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Commands.Batches;

    class RavenDbIngestionUnitOfWork : IngestionUnitOfWorkBase
    {
        readonly IDocumentStore store;
        readonly ConcurrentBag<ICommandData> commands;

        public RavenDbIngestionUnitOfWork(IDocumentStore store)
        {
            this.store = store;
            commands = new ConcurrentBag<ICommandData>();
            Monitoring = new RavenDbMonitoringIngestionUnitOfWork(this);
            Recoverability = new RavenDbRecoverabilityIngestionUnitOfWork(this);
        }

        internal void AddCommand(ICommandData command) => commands.Add(command);

        public override async Task Complete()
        {
            using (var session = store.OpenAsyncSession())
            {
                // not really interested in the batch results since a batch is atomic
                session.Advanced.Defer(commands.ToArray());
                await session.SaveChangesAsync();
            }
        }
    }
}