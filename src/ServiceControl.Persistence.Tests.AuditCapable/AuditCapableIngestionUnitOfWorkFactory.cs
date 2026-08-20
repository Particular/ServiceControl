namespace ServiceControl.Persistence.Tests.AuditCapable
{
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Persistence.UnitOfWork;

    class AuditCapableIngestionUnitOfWorkFactory(IIngestionUnitOfWorkFactory inner, InMemoryAuditStore auditStore) : IIngestionUnitOfWorkFactory
    {
        public async ValueTask<IIngestionUnitOfWork> StartNew(CancellationToken cancellationToken = default) =>
            new AuditCapableIngestionUnitOfWork(await inner.StartNew(cancellationToken), auditStore);

        public bool CanIngestMore() => inner.CanIngestMore();

        // Whatever the persister this delegates to says: the audit rows it adds are appended, never
        // merged, so they do not change how concurrent batches settle.
        public bool SupportsConcurrentBatches => inner.SupportsConcurrentBatches;
    }
}
