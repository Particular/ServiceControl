namespace ServiceControl.Audit.Persistence
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Applies the configured audit retention period to the messages and saga snapshots that are already stored.
    /// Only a persister that stores an expiry per document at ingestion registers an implementation. A persister
    /// that applies the retention period when it removes expired messages does not need one.
    /// </summary>
    public interface IAuditRetentionApplier
    {
        /// <summary>
        /// Sets the expiry of every stored message and saga snapshot to its processed time plus <paramref name="retentionPeriod"/>.
        /// Returns the number of documents that were updated.
        /// </summary>
        Task<long> ApplyRetention(TimeSpan retentionPeriod, IProgress<long> progress, CancellationToken cancellationToken = default);
    }
}
