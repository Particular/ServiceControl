namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System.Threading;
    using System.Threading.Tasks;

    class RavenDb5Installer : IPersistenceInstaller
    {
        public RavenDb5Installer(IRavenDbPersistenceLifecycle lifecycle)
        {
            this.lifecycle = lifecycle;
        }

        public async Task Install(CancellationToken cancellationToken)
        {
            await lifecycle.Start(cancellationToken).ConfigureAwait(false);
            await lifecycle.Stop(cancellationToken).ConfigureAwait(false);
        }

        readonly IRavenDbPersistenceLifecycle lifecycle;
    }
}