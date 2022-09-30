namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents;

    class RavenDbPersistenceLifecycle : IPersistenceLifecycle
    {
        public RavenDbPersistenceLifecycle(EmbeddedDatabase database)
        {
            this.database = database;
        }

        public IDocumentStore DocumentStore
        {
            get; private set;
        }

        public async Task Start(CancellationToken cancellationToken)
        {
            DocumentStore = await database.Initialize().ConfigureAwait(false);
        }

        public Task Stop(CancellationToken cancellationToken)
        {
            DocumentStore?.Dispose();
            database?.Dispose();

            return Task.CompletedTask;
        }

        readonly EmbeddedDatabase database;
    }
}