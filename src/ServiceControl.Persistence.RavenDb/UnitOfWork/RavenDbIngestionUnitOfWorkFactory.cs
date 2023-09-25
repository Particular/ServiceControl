namespace ServiceControl.Persistence.RavenDb
{
    using System.Threading.Tasks;
    using Raven.Client;
    using ServiceControl.Operations.BodyStorage;
    using ServiceControl.Persistence.UnitOfWork;

    class RavenDbIngestionUnitOfWorkFactory : IIngestionUnitOfWorkFactory
    {
        readonly IDocumentStore store;
        readonly MinimumRequiredStorageState customCheckState;
        readonly BodyStorageEnricher bodyStorageEnricher;

        public RavenDbIngestionUnitOfWorkFactory(IDocumentStore store, MinimumRequiredStorageState customCheckState, BodyStorageEnricher bodyStorageEnricher)
        {
            this.store = store;
            this.customCheckState = customCheckState;
            this.bodyStorageEnricher = bodyStorageEnricher;
        }

        public ValueTask<IIngestionUnitOfWork> StartNew()
            => new ValueTask<IIngestionUnitOfWork>(new RavenDbIngestionUnitOfWork(store, bodyStorageEnricher));

        public bool CanIngestMore()
        {
            return customCheckState.CanIngestMore;
        }
    }
}