namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System.Threading;
    using System.Threading.Tasks;

    class RavenDBInstaller : IPersistenceInstaller
    {
        public RavenDBInstaller(IRavenDbPersistenceLifecycle lifecycle)
        {
            this.lifecycle = lifecycle;
        }

        public async Task Install(CancellationToken cancellationToken)
        {
            await lifecycle.Start(cancellationToken);
            await lifecycle.Stop(cancellationToken);
        }

        readonly IRavenDbPersistenceLifecycle lifecycle;
    }
}