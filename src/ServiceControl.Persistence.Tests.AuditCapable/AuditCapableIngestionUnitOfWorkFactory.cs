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
    }
}
