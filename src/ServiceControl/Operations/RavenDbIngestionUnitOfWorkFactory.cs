namespace ServiceControl.Operations
{
    using Raven.Client;

    class RavenDbIngestionUnitOfWorkFactory : IIngestionUnitOfWorkFactory
    {
        readonly IDocumentStore store;

        public RavenDbIngestionUnitOfWorkFactory(IDocumentStore store) => this.store = store;

        public IIngestionUnitOfWork StartNew() => new RavenDbIngestionUnitOfWork(store);
    }
}