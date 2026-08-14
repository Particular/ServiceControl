namespace ServiceControl.Persistence
{
    using System;

    public class RetryBatch
    {
        public required string Id { get; init; }
        public string? Context { get; init; }
        public string? StagingId { get; init; }
        public string? Originator { get; init; }
        public string? Classifier { get; init; }
        public DateTime StartTime { get; init; }
        public DateTime? Last { get; init; }
        public string? RequestId { get; init; }
        public int InitialBatchSize { get; init; }
        public RetryType RetryType { get; init; }
        public RetryBatchStatus Status { get; init; }

        /// <summary>
        /// The messages the batch still holds, which is what a forwarded batch is counted against.
        /// Lower than <see cref="InitialBatchSize"/> whenever another batch claimed a message first,
        /// or a message was gone by the time the batch was staged.
        /// </summary>
        public int MessageCount { get; init; }

        /// <summary>
        /// Audit attribution for the initiating operation, threaded from the audit headers stamped on the
        /// internal retry command. Per-message audit entries are emitted when the batch is staged and are
        /// correlated to the API's operation entry by <see cref="OperationId"/>. Null only for legacy
        /// in-flight commands sent without the headers.
        /// </summary>
        public string? InitiatedById { get; init; }

        /// <inheritdoc cref="InitiatedById"/>
        public string? InitiatedByName { get; init; }

        /// <inheritdoc cref="InitiatedById"/>
        public string? OperationId { get; init; }
    }
}
