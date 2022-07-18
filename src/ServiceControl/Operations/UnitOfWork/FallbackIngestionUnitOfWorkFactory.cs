namespace ServiceControl.Operations
{
    class FallbackIngestionUnitOfWorkFactory : IIngestionUnitOfWorkFactory
    {
        IIngestionUnitOfWorkFactory primary;
        IIngestionUnitOfWorkFactory secondary;

        public FallbackIngestionUnitOfWorkFactory(IIngestionUnitOfWorkFactory primary, IIngestionUnitOfWorkFactory secondary)
        {
            this.primary = primary;
            this.secondary = secondary;
        }

        public IIngestionUnitOfWork StartNew() => new FallbackIngestionUnitOfWork(
            primary.StartNew(),
            secondary.StartNew()
        );
    }
}