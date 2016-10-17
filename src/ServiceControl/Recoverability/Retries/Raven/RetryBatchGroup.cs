namespace ServiceControl.Recoverability
{
    public class RetryBatchGroup
    {
        public string RequestId { get; set; }

        public RetryType RetryType { get; set; }

        public RetryBatchStatus Status { get; set; }

        public int InitialBatchSize { get; set; }
    }
}