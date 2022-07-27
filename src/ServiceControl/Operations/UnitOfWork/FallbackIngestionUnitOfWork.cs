namespace ServiceControl.Operations
{
    using System.Threading.Tasks;

    // HINT: This allows an implementor to provide only part of the implementation and allow the other part
    // to be handled by an existing implementation. This way a new persistence does not need to cover both
    // recoverability and monitoring. It can focus on one at a time.
    class FallbackIngestionUnitOfWork : IngestionUnitOfWorkBase
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

        public override Task Complete() => Task.WhenAll(primary.Complete(), fallback.Complete());

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                primary?.Dispose();
                fallback?.Dispose();
            }
        }
    }
}