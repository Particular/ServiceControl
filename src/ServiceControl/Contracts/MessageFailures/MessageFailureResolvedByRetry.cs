namespace ServiceControl.Contracts.MessageFailures
{
    public class MessageFailureResolvedByRetry : MessageFailureResolved
    {
        public string[] AlternativeFailedMessageIds { get; set; }
    }
}