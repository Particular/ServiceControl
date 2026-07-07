namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;

    public class RetryBatch
    {
        public string Id { get; set; }
        public string Context { get; set; }
        public string RetrySessionId { get; set; }
        public string StagingId { get; set; }
        public string Originator { get; set; }
        public string Classifier { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? Last { get; set; }
        public string RequestId { get; set; }
        public int InitialBatchSize { get; set; }
        public RetryType RetryType { get; set; }
        public RetryBatchStatus Status { get; set; }
        public IList<string> FailureRetries { get; set; } = [];

        // Audit attribution for the initiating operation, threaded from the audit headers stamped on the
        // internal retry command. Per-message audit entries are emitted when the batch is staged and are
        // correlated to the API's operation entry by OperationId. Null only for legacy in-flight commands
        // sent without the headers.
        public string InitiatedById { get; set; }
        public string InitiatedByName { get; set; }
        public string OperationId { get; set; }

        public static string MakeDocumentId(string messageUniqueId) => "RetryBatches/" + messageUniqueId;
    }
}