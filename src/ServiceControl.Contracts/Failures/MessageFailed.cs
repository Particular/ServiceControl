namespace ServiceControl.Contracts.Failures
{
    public class MessageFailed
    {
        public string MessageId { get; set; }
        public int NumberOfProcessingAttempts { get; set; }
    }
}