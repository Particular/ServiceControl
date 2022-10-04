namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System.Threading;
    using System.Threading.Tasks;
    using RavenDb5;

    class RavenDb5Installer : IPersistenceInstaller
    {
        public RavenDb5Installer(IRavenDbPersistenceLifecycle lifecycle, DatabaseSetup databaseSetup)
        {
            this.lifecycle = lifecycle;
            this.databaseSetup = databaseSetup;
        }

        public async Task Install(CancellationToken cancellationToken)
        {
            await lifecycle.Start(cancellationToken).ConfigureAwait(false);

            try
            {
                using (var documentStore = lifecycle.GetDocumentStore())
                {
                    await databaseSetup.Execute(documentStore, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            finally
            {
                await lifecycle.Stop(cancellationToken).ConfigureAwait(false);
            }
        }

        readonly IRavenDbPersistenceLifecycle lifecycle;
        readonly DatabaseSetup databaseSetup;
    }
}