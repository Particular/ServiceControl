namespace ServiceControl.Operations
{
    using System.Threading.Tasks;

    class FallbackIngestionUnitOfWork : IIngestionUnitOfWork
    {
        IIngestionUnitOfWork primary;
        IIngestionUnitOfWork fallback;

        public FallbackIngestionUnitOfWork(IIngestionUnitOfWork primary, IIngestionUnitOfWork fallback)
        {
            this.primary = primary;
            this.fallback = fallback;
            Monitoring = primary.Monitoring ?? fallback.Monitoring;
            Recoverability = primary.Recoverability ?? fallback.Recoverability;
        }

        public IMonitoringIngestionUnitOfWork Monitoring { get; }
        public IRecoverabilityIngestionUnitOfWork Recoverability { get; }

        public Task Complete() => Task.WhenAll(primary.Complete(), fallback.Complete());

        public void Dispose()
        {
            primary?.Dispose();
            fallback?.Dispose();
        }
    }
}