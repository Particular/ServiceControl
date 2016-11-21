namespace ServiceControl.Contracts.MessageFailures
{
    public class MessageFailureResolvedByRetry : MessageFailureResolved
    {
        public string AlternativeFailedMessageId { get; set; }
    }
}