namespace ServiceControl.Persistence.UnitOfWork
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IIngestionUnitOfWorkFactory
    {
        ValueTask<IIngestionUnitOfWork> StartNew(CancellationToken cancellationToken = default);
        bool CanIngestMore();

        /// <summary>
        /// Whether several ingestion batches may be written at once. Batches commit in whatever
        /// order they finish, so this is only true of a storage whose writes settle the same way
        /// however they interleave.
        /// </summary>
        bool SupportsConcurrentBatches { get; }
    }
}