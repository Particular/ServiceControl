namespace ServiceControl.Operations
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
            var primaryUnitOfWork = await primary.StartNew().ConfigureAwait(false);
            var secondaryUnitOfWork = await secondary.StartNew().ConfigureAwait(false);

            return new FallbackIngestionUnitOfWork(
                primaryUnitOfWork,
                secondaryUnitOfWork
            );
        }
    }
}