namespace ServiceControl.Persistence.RavenDb
{
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using Raven.Abstractions.Commands;
    using Raven.Client;
    using ServiceControl.Operations.BodyStorage;
    using ServiceControl.Persistence.UnitOfWork;

    class RavenDbIngestionUnitOfWork : IngestionUnitOfWorkBase
    {
        readonly IDocumentStore store;
        readonly ConcurrentBag<ICommandData> commands;

        public RavenDbIngestionUnitOfWork(IDocumentStore store, BodyStorageEnricher bodyStorageEnricher)
        {
            this.store = store;
            commands = new ConcurrentBag<ICommandData>();
            Monitoring = new RavenDbMonitoringIngestionUnitOfWork(this);
            Recoverability = new RavenDbRecoverabilityIngestionUnitOfWork(this, bodyStorageEnricher);
        }

        internal void AddCommand(ICommandData command) => commands.Add(command);

        public override Task Complete() =>
            // not really interested in the batch results since a batch is atomic
            store.AsyncDatabaseCommands.BatchAsync(commands);
    }
}