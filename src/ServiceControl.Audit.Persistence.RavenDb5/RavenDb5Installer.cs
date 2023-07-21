namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System.Threading;
    using System.Threading.Tasks;

    class RavenDb5Installer : IPersistenceInstaller
    {
        public RavenDb5Installer(IRavenDbPersistenceLifecycle lifecycle, DatabaseSetup databaseSetup)
        {
            this.lifecycle = lifecycle;
            this.databaseSetup = databaseSetup;
        }

        public async Task Install(CancellationToken cancellationToken)
        {
            await lifecycle.Start(cancellationToken);

            try
            {
                using (var documentStore = lifecycle.GetDocumentStore())
                {
                    await databaseSetup.Execute(documentStore, cancellationToken);
                }
            }
            finally
            {
                await lifecycle.Stop(cancellationToken);
            }
        }

        readonly IRavenDbPersistenceLifecycle lifecycle;
        readonly DatabaseSetup databaseSetup;
    }
}