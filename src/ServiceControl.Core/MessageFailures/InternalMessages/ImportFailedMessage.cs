namespace ServiceControl.MessageFailures.InternalMessages
{
    using ServiceControl.Contracts.Operations;

    public class ImportFailedMessage
    {
        public string UniqueMessageId { get; set; }
        public string FailingEndpointName { get; set; }
        public FailureDetails FailureDetails { get; set; }
        public string MessageType { get; set; }
        public string RetryId { get; set; }
    }
}