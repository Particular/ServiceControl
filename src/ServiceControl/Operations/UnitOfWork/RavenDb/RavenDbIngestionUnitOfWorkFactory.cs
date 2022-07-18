namespace ServiceControl.Operations
{
    using System.Threading.Tasks;
    using Raven.Client;

    class RavenDbIngestionUnitOfWorkFactory : IIngestionUnitOfWorkFactory
    {
        readonly IDocumentStore store;

        public RavenDbIngestionUnitOfWorkFactory(IDocumentStore store) => this.store = store;

        public Task<IIngestionUnitOfWork> StartNew()
            => Task.FromResult(
                (IIngestionUnitOfWork)new RavenDbIngestionUnitOfWork(store)
                );
    }
}