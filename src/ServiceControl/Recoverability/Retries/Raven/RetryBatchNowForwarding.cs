namespace ServiceControl.Recoverability
{
    public class RetryBatchNowForwarding
    {
        public const string Id = "RetryBatches/NowForwarding";
        public string RetryBatchId { get; set; }
        public string RetryOperationId { get; set; }
    }
}