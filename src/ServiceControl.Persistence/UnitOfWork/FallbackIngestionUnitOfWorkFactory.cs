namespace ServiceControl.Persistence.UnitOfWork
{
    using System.Threading.Tasks;

    class FallbackIngestionUnitOfWorkFactory : IIngestionUnitOfWorkFactory
    {
        IIngestionUnitOfWorkFactory primary;
        IIngestionUnitOfWorkFactory secondary;

        public FallbackIngestionUnitOfWorkFactory(IIngestionUnitOfWorkFactory primary, IIngestionUnitOfWorkFactory secondary)
        {
            this.primary = primary;
            this.secondary = secondary;
        }

        public async ValueTask<IIngestionUnitOfWork> StartNew()
        {
            var primaryUnitOfWork = await primary.StartNew();
            var secondaryUnitOfWork = await secondary.StartNew();

            return new FallbackIngestionUnitOfWork(
                primaryUnitOfWork,
                secondaryUnitOfWork
            );
        }

        public bool CanIngestMore()
        {
            return primary.CanIngestMore() && secondary.CanIngestMore();
        }
    }
}