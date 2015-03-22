namespace ServiceControl.Contracts.MessageFailures
{
    public class MessageFailureResolvedByRetry : MessageFailureResolved
    {
        public string FailedMessageType { get; set; }
    }
}