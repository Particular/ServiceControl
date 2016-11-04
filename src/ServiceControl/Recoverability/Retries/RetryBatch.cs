namespace ServiceControl.Recoverability
{
    using System.Collections.Generic;

    public class RetryBatch
    {
        public string Id { get; set; }
        public string Context { get; set; }
        public string RetrySessionId { get; set; }
        public string StagingId { get; set; }
        public string Originator { get; set; }
        public string RequestId { get; set; }
        public int InitialBatchSize { get; set; }
        public RetryType RetryType { get; set; }
        public RetryBatchStatus Status { get; set; }
        public IList<string> FailureRetries { get; set; }

        public RetryBatch()
        {
            FailureRetries = new List<string>();
        }

        public static string MakeDocumentId(string messageUniqueId)
        {
            return "RetryBatches/" + messageUniqueId;
        }
    }
}