namespace ServiceControl.Recoverability
{
    public class FailedMessageRetry
    {
        public string Id { get; set; }
        public string FailedMessageId { get; set; }
        public string RetryBatchId { get; set; }
        public int StageAttempts { get; set; }
    }
}