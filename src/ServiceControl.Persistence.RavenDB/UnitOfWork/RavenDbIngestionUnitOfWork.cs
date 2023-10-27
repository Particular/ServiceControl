namespace ServiceControl.Persistence.RavenDB
{
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Commands.Batches;
    using RavenDB;
    using ServiceControl.Persistence.UnitOfWork;

    class RavenDbIngestionUnitOfWork : IngestionUnitOfWorkBase
    {
        readonly IDocumentStore store;
        // Must be ordered - can't put attachments until after document exists
        readonly ConcurrentQueue<ICommandData> commands;

        public RavenDbIngestionUnitOfWork(IDocumentStore store, ExpirationManager expirationManager, RavenDBPersisterSettings settings)
        {
            this.store = store;
            commands = new ConcurrentQueue<ICommandData>();
            Monitoring = new RavenDbMonitoringIngestionUnitOfWork(this);
            Recoverability = new RavenDbRecoverabilityIngestionUnitOfWork(this, expirationManager, settings);
        }

        internal void AddCommand(ICommandData command) => commands.Enqueue(command);

        public override async Task Complete()
        {
            using (var session = store.OpenAsyncSession())
            {
                // not really interested in the batch results since a batch is atomic
                var commands = this.commands.ToArray();
                session.Advanced.Defer(commands);
                await session.SaveChangesAsync();
            }
        }
    }
}