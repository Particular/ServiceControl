namespace ServiceControl.Recoverability
{
    public class QueuedRetryItem
    {
        public RetryType RetryType { get; set; }
        public string RequestId { get; set; }
        public int Position { get; set; }
    }
}