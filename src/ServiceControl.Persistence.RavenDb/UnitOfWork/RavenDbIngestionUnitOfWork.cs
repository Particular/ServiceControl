namespace ServiceControl.Persistence.RavenDb
{
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using Operations;
    using Raven.Abstractions.Commands;
    using Raven.Client;

    class RavenDbIngestionUnitOfWork : IIngestionUnitOfWork
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

        public IMonitoringIngestionUnitOfWork Monitoring { get; }
        public IRecoverabilityIngestionUnitOfWork Recoverability { get; }

        public Task Complete() =>
            // not really interested in the batch results since a batch is atomic
            store.AsyncDatabaseCommands.BatchAsync(commands);

        public void Dispose()
        {
        }
    }
}