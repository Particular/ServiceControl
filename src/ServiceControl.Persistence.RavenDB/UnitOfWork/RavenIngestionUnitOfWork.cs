namespace ServiceControl.Persistence.RavenDB
{
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents.Commands.Batches;
    using ServiceControl.Persistence.UnitOfWork;

    class RavenIngestionUnitOfWork : IngestionUnitOfWorkBase
    {
        readonly IRavenSessionProvider sessionProvider;
        // Must be ordered - can't put attachments until after document exists
        readonly ConcurrentQueue<ICommandData> commands;

        public RavenIngestionUnitOfWork(IRavenSessionProvider sessionProvider, ExpirationManager expirationManager, RavenPersisterSettings settings)
        {
            this.sessionProvider = sessionProvider;
            commands = new ConcurrentQueue<ICommandData>();
            Monitoring = new RavenMonitoringIngestionUnitOfWork(this);
            Recoverability = new RavenRecoverabilityIngestionUnitOfWork(this, expirationManager, settings);
        }

        internal void AddCommand(ICommandData command) => commands.Enqueue(command);

        public override async Task Complete(CancellationToken cancellationToken)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            // not really interested in the batch results since a batch is atomic
            session.Advanced.Defer(commands.ToArray());
            await session.SaveChangesAsync(cancellationToken);
        }
    }
}