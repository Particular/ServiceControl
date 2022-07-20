namespace ServiceControl.Persistence.RavenDb
{
    using System.Threading.Tasks;
    using Operations;
    using Raven.Client;

    class RavenDbIngestionUnitOfWorkFactory : IIngestionUnitOfWorkFactory
    {
        readonly IDocumentStore store;

        public RavenDbIngestionUnitOfWorkFactory(IDocumentStore store) => this.store = store;

        public ValueTask<IIngestionUnitOfWork> StartNew()
            => new ValueTask<IIngestionUnitOfWork>(new RavenDbIngestionUnitOfWork(store));
    }
}