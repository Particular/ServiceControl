namespace ServiceControl.Audit.Persistence.UnitOfWork
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IAuditIngestionUnitOfWorkFactory
    {
        ValueTask<IAuditIngestionUnitOfWork> StartNew(int batchSize, CancellationToken cancellationToken = default); //Throws if not enough space or some other problem preventing from writing data
        bool CanIngestMore();

        /// <summary>
        /// Whether several ingestion batches may be written at once. Batches commit in whatever
        /// order they finish, so this is only true of a storage whose writes settle the same way
        /// however they interleave.
        /// </summary>
        bool SupportsConcurrentBatches { get; }
    }
}