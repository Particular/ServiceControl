namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System.Threading;
    using System.Threading.Tasks;

    class RavenInstaller : IPersistenceInstaller
    {
        public RavenInstaller(IRavenPersistenceLifecycle lifecycle)
        {
            this.lifecycle = lifecycle;
        }

        public async Task Install(CancellationToken cancellationToken)
        {
            await lifecycle.Start(cancellationToken);
            await lifecycle.Stop(cancellationToken);
        }

        readonly IRavenPersistenceLifecycle lifecycle;
    }
}