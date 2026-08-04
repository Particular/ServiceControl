#nullable enable
namespace ServiceControl.Persistence
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The batch lifecycle a retry goes through once it has been created: staged, forwarded, done.
    /// Every member is atomic on its own, so nothing is left half applied by a crash between calls.
    /// </summary>
    public interface IRetryStagingStore
    {
        /// <summary>
        /// The batch waiting to be staged, or null when there is nothing to stage.
        /// </summary>
        Task<RetryBatch?> GetStagingBatch();

        /// <summary>
        /// The messages the batch still holds. A message that has since been deleted is not returned,
        /// so the result can be shorter than what the batch claimed.
        /// </summary>
        Task<StagingMessage[]> GetMessagesToStage(string batchId);

        /// <summary>
        /// Hands the batch to the forwarder: the batch keeps only the messages that were staged, those
        /// messages become <see cref="MessageFailures.FailedMessageStatus.RetryIssued"/>, and the batch
        /// becomes the one being forwarded.
        /// </summary>
        Task MarkBatchAsForwarding(string batchId, string stagingId, IReadOnlyCollection<string> stagedMessageIds);

        /// <summary>
        /// Drops a batch that has nothing left to stage, because every message it covered was claimed
        /// by an earlier batch.
        /// </summary>
        Task DiscardBatch(string batchId);

        /// <summary>
        /// The batch being forwarded, or null when none is. Outlives the batch it names, so the batch
        /// itself can be gone by the time it is asked for.
        /// </summary>
        Task<string?> GetForwardingBatchId();

        Task<RetryBatch?> GetBatch(string batchId, CancellationToken cancellationToken);

        /// <summary>
        /// Removes the forwarded batch and the pointer to it. Tolerates a batch that is already gone,
        /// so a pointer left behind by a premature shutdown can always be cleared.
        /// </summary>
        Task CompleteForwarding(string batchId);

        /// <summary>
        /// Records that the whole batch failed to reach the transport, so the next attempt at these
        /// messages knows it is a retry of a retry.
        /// </summary>
        Task RecordStagingFailure(IReadOnlyCollection<string> uniqueMessageIds);

        Task IncrementStagingAttempts(string uniqueMessageId);

        /// <summary>
        /// Releases the message from its batch, leaving it unresolved for a later retry request.
        /// </summary>
        Task RemoveFromBatch(string uniqueMessageId);
    }
}
